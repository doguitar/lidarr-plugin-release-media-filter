using NSubstitute;
using NzbDrone.Core.Music;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Plugins;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class ReleaseMediaFilterNotificationTests
{
    [Fact]
    public void OnReleaseImport_skips_when_settings_are_missing()
    {
        var filterService = Substitute.For<IReleaseFilterService>();
        var settingsResolver = Substitute.For<IReleaseMediaFilterSettingsResolver>();
        settingsResolver.Resolve().Returns((FilterOptions?)null);
        var subject = new ReleaseMediaFilterNotification(
            filterService,
            settingsResolver,
            NLog.LogManager.GetLogger("test"));

        subject.OnReleaseImport(new AlbumDownloadMessage { Album = new Album { Id = 9, Title = "Album" } });

        filterService.DidNotReceiveWithAnyArgs().FilterAlbum(default, default!);
    }

    [Fact]
    public void OnReleaseImport_filters_with_resolved_options()
    {
        var filterService = Substitute.For<IReleaseFilterService>();
        var settingsResolver = Substitute.For<IReleaseMediaFilterSettingsResolver>();
        var options = new FilterOptions(FilterMode.Blacklist, new[] { "Vinyl" }, NoAllowedReleaseAction.KeepLastResort, true);
        settingsResolver.Resolve().Returns(options);
        var subject = new ReleaseMediaFilterNotification(
            filterService,
            settingsResolver,
            NLog.LogManager.GetLogger("test"));

        subject.OnReleaseImport(new AlbumDownloadMessage { Album = new Album { Id = 9, Title = "Album" } });

        filterService.Received(1).FilterAlbum(9, options);
    }
}
