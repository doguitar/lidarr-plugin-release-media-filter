using NSubstitute;
using NzbDrone.Core.Music;
using NzbDrone.Core.Plugins;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class ReleaseMediaFilterScanTaskTests
{
    [Fact]
    public void Execute_skips_when_settings_are_missing()
    {
        var filterService = Substitute.For<IReleaseFilterService>();
        var artistService = Substitute.For<IArtistService>();
        var settingsResolver = Substitute.For<IReleaseMediaFilterSettingsResolver>();
        settingsResolver.Resolve().Returns((FilterOptions?)null);
        settingsResolver.ResolveScanIntervalMinutes().Returns(1440);

        var subject = new ReleaseMediaFilterScanTask(
            filterService,
            artistService,
            settingsResolver,
            NLog.LogManager.GetLogger("test"));

        var command = new ReleaseMediaFilterScanCommand();
        subject.Execute(command);

        artistService.DidNotReceive().GetAllArtists();
        filterService.DidNotReceiveWithAnyArgs().FilterArtist(default!, default!);
        Assert.Contains("not configured", command.ResultMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_scans_unmonitored_artists()
    {
        var filterService = Substitute.For<IReleaseFilterService>();
        var artistService = Substitute.For<IArtistService>();
        var settingsResolver = Substitute.For<IReleaseMediaFilterSettingsResolver>();
        var options = new FilterOptions(FilterMode.Blacklist, new[] { "Vinyl" }, NoAllowedReleaseAction.KeepLastResort, true);
        settingsResolver.Resolve().Returns(options);
        var unmonitored = new Artist { Id = 5, Name = "B-sides", Monitored = false };
        artistService.GetAllArtists().Returns(new List<Artist> { unmonitored });
        filterService.FilterArtist(unmonitored, options).Returns(FilterResult.Empty);

        var subject = new ReleaseMediaFilterScanTask(
            filterService,
            artistService,
            settingsResolver,
            NLog.LogManager.GetLogger("test"));

        subject.Execute(new ReleaseMediaFilterScanCommand());

        filterService.Received(1).FilterArtist(unmonitored, options);
    }
}
