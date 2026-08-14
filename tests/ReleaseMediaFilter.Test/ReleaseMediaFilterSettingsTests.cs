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
}
