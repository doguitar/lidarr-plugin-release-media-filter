using NzbDrone.Core.Music;
using NzbDrone.Core.Plugins;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class ReleasePreferenceTests
{
    private static AlbumRelease Release(
        int id,
        string format,
        int trackCount = 10,
        string? title = null,
        params string[] countries)
    {
        return new AlbumRelease
        {
            Id = id,
            AlbumId = 1,
            Title = title ?? $"Release {id}",
            TrackCount = trackCount,
            Country = countries.ToList(),
            Media = new List<Medium> { new() { Number = 1, Name = format, Format = format } }
        };
    }

    private static FilterOptions Options(params ReleaseSortRule[] rules) =>
        new(FilterMode.Blacklist, new[] { "Vinyl" }, NoAllowedReleaseAction.KeepLastResort, true, false, rules);

    [Fact]
    public void Default_prefers_digital_then_cd_then_more_tracks()
    {
        var vinyl = Release(1, "Vinyl", trackCount: 20);
        var cd = Release(2, "CD", trackCount: 12);
        var digital = Release(3, "Digital Media", trackCount: 11);

        var preferred = ReleasePreference.Pick(new[] { vinyl, cd, digital }, options: null);

        Assert.Equal(digital.Id, preferred.Id);
    }

    [Fact]
    public void Medium_regex_then_fewest_tracks_then_country_regex()
    {
        var usCdLong = Release(1, "CD", trackCount: 18, countries: "US");
        var gbCdShort = Release(2, "CD", trackCount: 10, countries: "GB");
        var jpCdShort = Release(3, "CD", trackCount: 10, countries: "JP");
        var digital = Release(4, "Digital Media", trackCount: 8, countries: "XW");

        var options = Options(
            new ReleaseSortRule(ReleaseSortField.MediumRegex, ReleaseSortDirection.Descending, "^CD$"),
            new ReleaseSortRule(ReleaseSortField.TrackCount, ReleaseSortDirection.Ascending, null),
            new ReleaseSortRule(ReleaseSortField.CountryRegex, ReleaseSortDirection.Descending, "US|GB|UK"));

        var ranked = ReleasePreference.Rank(new[] { usCdLong, gbCdShort, jpCdShort, digital }, options);

        Assert.Equal(new[] { gbCdShort.Id, jpCdShort.Id, usCdLong.Id, digital.Id }, ranked.Select(r => r.Id));
        Assert.Equal(gbCdShort.Id, ranked[0].Id);
        Assert.Equal(ranked[0].Id, ReleasePreference.Pick(new[] { usCdLong, gbCdShort, jpCdShort, digital }, options).Id);
    }

    [Fact]
    public void Title_ascending_breaks_ties()
    {
        var b = Release(1, "CD", title: "B");
        var a = Release(2, "CD", title: "A");

        var options = Options(new ReleaseSortRule(ReleaseSortField.Title, ReleaseSortDirection.Ascending, null));
        var preferred = ReleasePreference.Pick(new[] { b, a }, options);

        Assert.Equal(a.Id, preferred.Id);
    }

    [Fact]
    public void Medium_sort_is_case_insensitive()
    {
        var vinyl = Release(1, "Vinyl");
        var cd = Release(2, "cd");

        var options = Options(new ReleaseSortRule(ReleaseSortField.Medium, ReleaseSortDirection.Ascending, null));
        var preferred = ReleasePreference.Pick(new[] { vinyl, cd }, options);

        Assert.Equal(cd.Id, preferred.Id);
    }

    [Fact]
    public void Two_x_cd_matches_cd_token_regex()
    {
        var vinyl = Release(1, "Vinyl");
        var doubleCd = Release(2, "2xCD");

        var options = Options(new ReleaseSortRule(ReleaseSortField.MediumRegex, ReleaseSortDirection.Descending, "^CD$"));
        var preferred = ReleasePreference.Pick(new[] { vinyl, doubleCd }, options);

        Assert.Equal(doubleCd.Id, preferred.Id);
    }

    [Fact]
    public void Preview_marks_first_remaining_release_as_monitored()
    {
        var settings = new ReleaseMediaFilterSettings
        {
            SortField1 = ReleaseSortField.MediumRegex,
            SortDirection1 = ReleaseSortDirection.Descending,
            SortPattern1 = "^CD$",
            SortField2 = ReleaseSortField.TrackCount,
            SortDirection2 = ReleaseSortDirection.Ascending,
            SortField3 = ReleaseSortField.CountryRegex,
            SortDirection3 = ReleaseSortDirection.Descending,
            SortPattern3 = "US|GB|UK"
        };

        var preview = ReleasePreference.Preview(settings.ToFilterOptions());
        var first = preview[0];

        Assert.Equal("would be monitored", first.Hint);
        Assert.Contains("GB CD (10 tracks)", first.Name);
        Assert.Contains(preview, option => option.Hint == "would be deleted" && option.Name.Contains("Vinyl"));
    }
}
