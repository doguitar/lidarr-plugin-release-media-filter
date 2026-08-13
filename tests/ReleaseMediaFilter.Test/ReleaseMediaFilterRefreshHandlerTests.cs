using NSubstitute;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Plugins;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class ReleaseMediaFilterRefreshHandlerTests
{
    [Fact]
    public void AlbumUpdatedEvent_filters_that_album()
    {
        var filterService = Substitute.For<IReleaseFilterService>();
        var settingsResolver = Substitute.For<IReleaseMediaFilterSettingsResolver>();
        var options = new FilterOptions(FilterMode.Blacklist, new[] { "Vinyl" }, NoAllowedReleaseAction.DeleteFiltered, true);
        settingsResolver.Resolve().Returns(options);
        filterService.FilterAlbum(42, options).Returns(new FilterResult { ReleasesDeleted = 1, ReleasesInspected = 2 });

        var handler = new ReleaseMediaFilterRefreshHandler(
            filterService,
            settingsResolver,
            NLog.LogManager.GetLogger("test"));

        handler.Handle(new AlbumUpdatedEvent(new Album { Id = 42, Title = "Test Album" }));

        filterService.Received(1).FilterAlbum(42, options);
    }

    [Fact]
    public void AlbumUpdatedEvent_ignores_null_album()
    {
        var filterService = Substitute.For<IReleaseFilterService>();
        var settingsResolver = Substitute.For<IReleaseMediaFilterSettingsResolver>();
        var handler = new ReleaseMediaFilterRefreshHandler(
            filterService,
            settingsResolver,
            NLog.LogManager.GetLogger("test"));

        handler.Handle(new AlbumUpdatedEvent(null!));

        filterService.DidNotReceiveWithAnyArgs().FilterAlbum(default, default!);
    }
}
