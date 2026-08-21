using NzbDrone.Core.Plugins;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class ReleaseMediaFilterSettingsTests
{
    [Fact]
    public void Defaults_are_vinyl_cassette_blacklist()
    {
        var settings = new ReleaseMediaFilterSettings();
        var options = settings.ToFilterOptions();

        Assert.Equal(FilterMode.Blacklist, options.Mode);
        Assert.Contains("Vinyl", options.MediaTypes);
        Assert.Contains("Cassette", options.MediaTypes);
        Assert.Equal(NoAllowedReleaseAction.KeepLastResort, options.NoAllowedReleaseAction);
        Assert.True(options.SkipReleasesWithFiles);
        Assert.False(options.SearchAfterFileCleanup);
        Assert.Empty(options.SortRules);
        Assert.Equal(ReleaseSortField.None, settings.SortField1);
        Assert.Equal(1440, settings.ScanIntervalMinutes);
    }

    [Fact]
    public void ParseMediaTypes_splits_and_deduplicates()
    {
        var parsed = ReleaseMediaFilterSettings.ParseMediaTypes(" Vinyl, cassette, VINYL , CD ");

        Assert.Equal(3, parsed.Count);
        Assert.Contains("Vinyl", parsed, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("cassette", parsed, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("CD", parsed, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseMediaTypes_returns_empty_when_blank()
    {
        Assert.Empty(ReleaseMediaFilterSettings.ParseMediaTypes("   "));
        Assert.Empty(ReleaseMediaFilterSettings.ParseMediaTypes(null));
    }

    [Fact]
    public void Validate_rejects_empty_media_types()
    {
        var settings = new ReleaseMediaFilterSettings { MediaTypes = "  " };
        var result = settings.Validate();

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_rejects_short_scan_interval()
    {
        var settings = new ReleaseMediaFilterSettings { ScanIntervalMinutes = 15 };
        var result = settings.Validate();

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_accepts_minimum_scan_interval()
    {
        var settings = new ReleaseMediaFilterSettings { ScanIntervalMinutes = 60 };
        var result = settings.Validate();

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_warns_when_whitelist_uses_vinyl_cassette_defaults()
    {
        var settings = new ReleaseMediaFilterSettings { FilterMode = FilterMode.Whitelist };
        var result = settings.Validate();

        Assert.True(result.IsValid);
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void Validate_rejects_invalid_sort_regex()
    {
        var settings = new ReleaseMediaFilterSettings
        {
            SortField1 = ReleaseSortField.MediumRegex,
            SortPattern1 = "("
        };

        Assert.False(settings.Validate().IsValid);
    }

    [Fact]
    public void Validate_rejects_regex_sort_without_pattern()
    {
        var settings = new ReleaseMediaFilterSettings
        {
            SortField1 = ReleaseSortField.CountryRegex,
            SortPattern1 = " "
        };

        Assert.False(settings.Validate().IsValid);
    }

    [Fact]
    public void ToFilterOptions_includes_active_sort_rules()
    {
        var settings = new ReleaseMediaFilterSettings
        {
            SortField1 = ReleaseSortField.MediumRegex,
            SortDirection1 = ReleaseSortDirection.Descending,
            SortPattern1 = "^CD$",
            SortField2 = ReleaseSortField.TrackCount,
            SortDirection2 = ReleaseSortDirection.Ascending
        };

        var rules = settings.ToFilterOptions().SortRules;

        Assert.Equal(2, rules.Count);
        Assert.Equal(ReleaseSortField.MediumRegex, rules[0].Field);
        Assert.Equal("^CD$", rules[0].Pattern);
        Assert.Equal(ReleaseSortField.TrackCount, rules[1].Field);
        Assert.Equal(ReleaseSortDirection.Ascending, rules[1].Direction);
    }
}
