using NSubstitute;
using NzbDrone.Core.Annotations;
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

    [Fact]
    public void Test_rejects_empty_media_types()
    {
        var subject = new ReleaseMediaFilterNotification(
            Substitute.For<IReleaseFilterService>(),
            Substitute.For<IReleaseMediaFilterSettingsResolver>(),
            NLog.LogManager.GetLogger("test"))
        {
            Definition = new NotificationDefinition
            {
                Settings = new ReleaseMediaFilterSettings { MediaTypes = " " }
            }
        };

        Assert.False(subject.Test().IsValid);
    }

    [Fact]
    public void RequestAction_previewSort_returns_ranked_sample_options()
    {
        var subject = new ReleaseMediaFilterNotification(
            Substitute.For<IReleaseFilterService>(),
            Substitute.For<IReleaseMediaFilterSettingsResolver>(),
            NLog.LogManager.GetLogger("test"))
        {
            Definition = new NotificationDefinition
            {
                Settings = new ReleaseMediaFilterSettings
                {
                    SortField1 = ReleaseSortField.TrackCount,
                    SortDirection1 = ReleaseSortDirection.Ascending
                }
            }
        };

        var result = subject.RequestAction(ReleasePreference.PreviewAction, new Dictionary<string, string>());
        var options = result.GetType().GetProperty("options")!.GetValue(result) as IEnumerable<FieldSelectOption>;

        Assert.NotNull(options);
        var list = options!.ToList();
        Assert.Equal("would be monitored", list[0].Hint);
        Assert.Contains(list, option => option.Hint == "would be deleted");
    }
}
