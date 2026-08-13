using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

public static class MediaTypeMatcher
{
    private static readonly HashSet<string> ShortTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "CD",
        "DAT",
        "DVD",
        "SACD"
    };

    public static IReadOnlyList<string> GetFormats(AlbumRelease release)
    {
        if (release?.Media == null)
        {
            return Array.Empty<string>();
        }

        return release.Media
            .Select(medium => medium?.Format)
            .Where(format => !string.IsNullOrWhiteSpace(format))
            .Cast<string>()
            .ToList();
    }

    public static bool IsReleaseFiltered(AlbumRelease release, FilterOptions options)
    {
        return IsFiltered(GetFormats(release), options);
    }

    public static bool IsFiltered(IEnumerable<string?> formats, FilterOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.MediaTypes.Count == 0)
        {
            return false;
        }

        var knownFormats = (formats ?? Array.Empty<string?>())
            .Where(format => !string.IsNullOrWhiteSpace(format))
            .Select(format => format!.Trim())
            .ToList();

        if (knownFormats.Count == 0)
        {
            return false;
        }

        if (options.Mode == FilterMode.Blacklist)
        {
            return knownFormats.Any(format => MatchesAnyMediaType(format, options.MediaTypes));
        }

        return knownFormats.Any(format => !MatchesAnyMediaType(format, options.MediaTypes));
    }

    public static bool MatchesAnyMediaType(string format, IEnumerable<string> mediaTypes)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return false;
        }

        foreach (var mediaType in mediaTypes)
        {
            if (FormatMatches(format, mediaType))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool FormatMatches(string format, string mediaType)
    {
        if (string.IsNullOrWhiteSpace(format) || string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        var normalizedFormat = format.Trim().ToLowerInvariant();
        var normalizedType = mediaType.Trim().ToLowerInvariant();

        if (ShortTokens.Contains(mediaType.Trim()))
        {
            var pattern = $@"(?:^|[^a-z0-9])(?:\d+x)?{Regex.Escape(normalizedType)}(?:[^a-z0-9]|$)";
            return Regex.IsMatch(normalizedFormat, pattern);
        }

        return normalizedFormat.Contains(normalizedType, StringComparison.Ordinal);
    }
}
