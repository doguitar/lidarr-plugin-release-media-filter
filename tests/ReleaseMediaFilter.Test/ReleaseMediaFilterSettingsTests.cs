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
        Assert.Equal(NoAllowedReleaseAction.DeleteFiltered, options.NoAllowedReleaseAction);
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
    public void ParseMediaTypes_falls_back_when_empty()
    {
        var parsed = ReleaseMediaFilterSettings.ParseMediaTypes("   ");

        Assert.Contains("Vinyl", parsed);
        Assert.Contains("Cassette", parsed);
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
}
