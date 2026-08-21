using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

public enum ReleaseSortField
{
    [FieldOption(Label = "None", Hint = "Skip this sort slot.")]
    None = 0,

    [FieldOption(Label = "Title", Hint = "Album release title.")]
    Title = 1,

    [FieldOption(Label = "Country", Hint = "MusicBrainz country list, joined.")]
    Country = 2,

    [FieldOption(Label = "Track count", Hint = "Number of tracks on the release.")]
    TrackCount = 3,

    [FieldOption(Label = "Medium", Hint = "Medium formats, joined.")]
    Medium = 4,

    [FieldOption(Label = "Country regex", Hint = "Prefer releases whose country list matches the regex.")]
    CountryRegex = 5,

    [FieldOption(Label = "Medium regex", Hint = "Prefer releases whose medium formats match the regex.")]
    MediumRegex = 6
}

public enum ReleaseSortDirection
{
    [FieldOption(Label = "Ascending", Hint = "A–Z, fewer tracks first, or regex non-matches first.")]
    Ascending = 0,

    [FieldOption(Label = "Descending", Hint = "Z–A, more tracks first, or regex matches first.")]
    Descending = 1
}

public sealed class ReleaseSortRule
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    public ReleaseSortRule(ReleaseSortField field, ReleaseSortDirection direction, string? pattern)
    {
        Field = field;
        Direction = direction;
        Pattern = pattern?.Trim() ?? string.Empty;
        Regex = TryCompile(Field, Pattern);
    }

    public ReleaseSortField Field { get; }

    public ReleaseSortDirection Direction { get; }

    public string Pattern { get; }

    public Regex? Regex { get; }

    public bool IsActive => Field != ReleaseSortField.None;

    public static bool TryValidatePattern(ReleaseSortField field, string? pattern, out string? error)
    {
        error = null;
        if (field is not (ReleaseSortField.CountryRegex or ReleaseSortField.MediumRegex))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(pattern))
        {
            error = "Enter a regular expression when sorting by country or medium regex.";
            return false;
        }

        try
        {
            _ = new Regex(pattern.Trim(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            error = "The sort regular expression is invalid.";
            return false;
        }
    }

    private static Regex? TryCompile(ReleaseSortField field, string pattern)
    {
        if (field is not (ReleaseSortField.CountryRegex or ReleaseSortField.MediumRegex) ||
            string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        try
        {
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

public static class ReleasePreference
{
    private static readonly string[] PreferredFormatOrder =
    {
        "Digital Media",
        "CD"
    };

    public static AlbumRelease Pick(IReadOnlyList<AlbumRelease> releases, FilterOptions? options)
    {
        if (releases == null || releases.Count == 0)
        {
            throw new ArgumentException("At least one release is required.", nameof(releases));
        }

        var rules = options?.SortRules?.Where(rule => rule.IsActive).ToList() ?? new List<ReleaseSortRule>();
        if (rules.Count == 0)
        {
            return releases
                .OrderByDescending(LegacyScore)
                .ThenByDescending(release => release.TrackCount)
                .ThenBy(release => release.Id)
                .First();
        }

        IOrderedEnumerable<AlbumRelease>? ordered = null;
        foreach (var rule in rules)
        {
            ordered = Apply(ordered == null ? releases : ordered, rule, ordered == null);
        }

        return ordered!.ThenBy(release => release.Id).First();
    }

    private static IOrderedEnumerable<AlbumRelease> Apply(
        IEnumerable<AlbumRelease> source,
        ReleaseSortRule rule,
        bool first)
    {
        var descending = rule.Direction == ReleaseSortDirection.Descending;
        return rule.Field switch
        {
            ReleaseSortField.Title => Order(source, first, descending, release => release.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            ReleaseSortField.Country => Order(source, first, descending, CountryKey, StringComparer.OrdinalIgnoreCase),
            ReleaseSortField.TrackCount => Order(source, first, descending, release => release.TrackCount, Comparer<int>.Default),
            ReleaseSortField.Medium => Order(source, first, descending, MediumKey, StringComparer.OrdinalIgnoreCase),
            ReleaseSortField.CountryRegex => Order(source, first, descending, release => RegexHit(rule.Regex, Countries(release)), Comparer<int>.Default),
            ReleaseSortField.MediumRegex => Order(source, first, descending, release => RegexHit(rule.Regex, MediumValues(release)), Comparer<int>.Default),
            _ => first ? source.OrderBy(release => release.Id) : ((IOrderedEnumerable<AlbumRelease>)source).ThenBy(release => release.Id)
        };
    }

    private static IOrderedEnumerable<AlbumRelease> Order<T>(
        IEnumerable<AlbumRelease> source,
        bool first,
        bool descending,
        Func<AlbumRelease, T> key,
        IComparer<T> comparer)
    {
        if (first)
        {
            return descending ? source.OrderByDescending(key, comparer) : source.OrderBy(key, comparer);
        }

        var ordered = (IOrderedEnumerable<AlbumRelease>)source;
        return descending ? ordered.ThenByDescending(key, comparer) : ordered.ThenBy(key, comparer);
    }

    private static string CountryKey(AlbumRelease release) =>
        string.Join(',', Countries(release));

    private static string MediumKey(AlbumRelease release) =>
        string.Join('+', MediaTypeMatcher.GetFormats(release));

    private static IEnumerable<string> Countries(AlbumRelease release) =>
        release.Country ?? Enumerable.Empty<string>();

    private static IEnumerable<string> MediumValues(AlbumRelease release)
    {
        foreach (var format in MediaTypeMatcher.GetFormats(release))
        {
            yield return format;
            foreach (var token in MediaTypeMatcher.Tokenize(format))
            {
                yield return token;
            }
        }
    }

    private static int RegexHit(Regex? regex, IEnumerable<string> values)
    {
        if (regex == null)
        {
            return 0;
        }

        try
        {
            return values.Any(value => !string.IsNullOrWhiteSpace(value) && regex.IsMatch(value)) ? 1 : 0;
        }
        catch (RegexMatchTimeoutException)
        {
            return 0;
        }
    }

    private static int LegacyScore(AlbumRelease release)
    {
        var formats = MediaTypeMatcher.GetFormats(release);
        for (var i = 0; i < PreferredFormatOrder.Length; i++)
        {
            if (formats.Any(format => string.Equals(format, PreferredFormatOrder[i], StringComparison.OrdinalIgnoreCase)))
            {
                return PreferredFormatOrder.Length - i;
            }
        }

        return 0;
    }
}
