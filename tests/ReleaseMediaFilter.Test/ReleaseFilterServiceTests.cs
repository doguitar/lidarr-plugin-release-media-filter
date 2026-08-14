using NSubstitute;
using NzbDrone.Core.Music;
using NzbDrone.Core.Plugins;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class ReleaseFilterServiceTests
{
    private readonly IReleaseService _releaseService = Substitute.For<IReleaseService>();
    private readonly ITrackService _trackService = Substitute.For<ITrackService>();
    private readonly IAlbumService _albumService = Substitute.For<IAlbumService>();
    private readonly ReleaseFilterService _subject;

    public ReleaseFilterServiceTests()
    {
        _subject = new ReleaseFilterService(
            _releaseService,
            _trackService,
            _albumService,
            NLog.LogManager.GetLogger("test"));
    }

    private static FilterOptions Blacklist(NoAllowedReleaseAction fallback = NoAllowedReleaseAction.DeleteFiltered, bool skipFiles = true) =>
        new(FilterMode.Blacklist, new[] { "Vinyl", "Cassette" }, fallback, skipFiles);

    private static AlbumRelease Release(int id, string format, bool monitored = false, int trackCount = 10, string status = "Official")
    {
        return new AlbumRelease
        {
            Id = id,
            AlbumId = 1,
            Title = $"Release {id}",
            Status = status,
            TrackCount = trackCount,
            Monitored = monitored,
            Media = new List<Medium> { new() { Number = 1, Name = format, Format = format } }
        };
    }

    private static Track Track(int id, int releaseId, bool hasFile)
    {
        return new Track
        {
            Id = id,
            AlbumReleaseId = releaseId,
            TrackFileId = hasFile ? 99 : 0,
            Title = $"Track {id}"
        };
    }

    [Fact]
    public void Deletes_vinyl_and_cassette_and_monitors_digital()
    {
        var vinyl = Release(1, "Vinyl", monitored: true);
        var cassette = Release(2, "Cassette");
        var digital = Release(3, "Digital Media");
        var remaining = new List<AlbumRelease> { digital };

        _releaseService.GetReleasesByAlbum(1).Returns(
            _ => new List<AlbumRelease> { vinyl, cassette, digital },
            _ => remaining);
        _trackService.GetTracksByRelease(Arg.Any<int>()).Returns(new List<Track>());

        var result = _subject.FilterAlbum(1, Blacklist());

        _releaseService.Received(1).DeleteMany(Arg.Is<List<AlbumRelease>>(list =>
            list.Count == 2 && list.Any(r => r.Id == 1) && list.Any(r => r.Id == 2)));
        _releaseService.Received(1).SetMonitored(digital);
        Assert.Equal(2, result.ReleasesDeleted);
        Assert.Equal(1, result.MonitoredSwitched);
        Assert.Equal(0, result.ReleasesSkippedWithFiles);
    }

    [Fact]
    public void Last_resort_keeps_sole_vinyl()
    {
        var vinyl = Release(1, "Vinyl", monitored: true);
        _releaseService.GetReleasesByAlbum(1).Returns(new List<AlbumRelease> { vinyl });

        var result = _subject.FilterAlbum(1, Blacklist(NoAllowedReleaseAction.KeepLastResort));

        _releaseService.DidNotReceive().DeleteMany(Arg.Any<List<AlbumRelease>>());
        Assert.Equal(1, result.ReleasesKeptLastResort);
        Assert.Equal(0, result.ReleasesDeleted);
    }

    [Fact]
    public void Last_resort_keeps_one_vinyl_and_deletes_the_rest()
    {
        var vinylA = Release(1, "Vinyl", trackCount: 8);
        var vinylB = Release(2, "Vinyl", trackCount: 12);
        var vinylC = Release(3, "Vinyl", trackCount: 10);
        _releaseService.GetReleasesByAlbum(1).Returns(
            _ => new List<AlbumRelease> { vinylA, vinylB, vinylC },
            _ => new List<AlbumRelease> { vinylB });
        _trackService.GetTracksByRelease(Arg.Any<int>()).Returns(new List<Track>());

        var result = _subject.FilterAlbum(1, Blacklist(NoAllowedReleaseAction.KeepLastResort));

        _releaseService.Received(1).DeleteMany(Arg.Is<List<AlbumRelease>>(list =>
            list.Count == 2 && list.All(release => release.Id != 2)));
        Assert.Equal(2, result.ReleasesDeleted);
        Assert.Equal(1, result.ReleasesKeptLastResort);
    }

    [Fact]
    public void Default_deletes_sole_vinyl_when_no_alternative()
    {
        var vinyl = Release(1, "Vinyl", monitored: true);
        _releaseService.GetReleasesByAlbum(1).Returns(
            _ => new List<AlbumRelease> { vinyl },
            _ => new List<AlbumRelease>());
        _trackService.GetTracksByRelease(1).Returns(new List<Track> { Track(10, 1, hasFile: false) });

        var result = _subject.FilterAlbum(1, Blacklist());

        _trackService.Received(1).DeleteMany(Arg.Is<List<Track>>(tracks => tracks.Count == 1));
        _releaseService.Received(1).DeleteMany(Arg.Is<List<AlbumRelease>>(list => list.Single().Id == 1));
        _releaseService.DidNotReceive().SetMonitored(Arg.Any<AlbumRelease>());
        Assert.Equal(1, result.ReleasesDeleted);
    }

    [Fact]
    public void Skips_delete_when_release_has_imported_files()
    {
        var vinyl = Release(1, "Vinyl", monitored: true);
        var cd = Release(2, "CD");
        _releaseService.GetReleasesByAlbum(1).Returns(
            _ => new List<AlbumRelease> { vinyl, cd },
            _ => new List<AlbumRelease> { vinyl, cd });
        _trackService.GetTracksByRelease(1).Returns(new List<Track> { Track(10, 1, hasFile: true) });
        _trackService.GetTracksByRelease(2).Returns(new List<Track>());

        var result = _subject.FilterAlbum(1, Blacklist());

        _releaseService.DidNotReceive().DeleteMany(Arg.Any<List<AlbumRelease>>());
        _releaseService.Received(1).SetMonitored(cd);
        Assert.Equal(0, result.ReleasesDeleted);
        Assert.Equal(1, result.ReleasesSkippedWithFiles);
        Assert.Equal(1, result.MonitoredSwitched);
    }

    [Fact]
    public void Does_not_delete_release_with_files_even_when_skip_setting_is_off()
    {
        var vinyl = Release(1, "Vinyl", monitored: true);
        var cd = Release(2, "CD");
        _releaseService.GetReleasesByAlbum(1).Returns(
            _ => new List<AlbumRelease> { vinyl, cd },
            _ => new List<AlbumRelease> { vinyl, cd });
        _trackService.GetTracksByRelease(1).Returns(new List<Track>
        {
            Track(10, 1, hasFile: true),
            Track(11, 1, hasFile: false)
        });
        _trackService.GetTracksByRelease(2).Returns(new List<Track>());

        var result = _subject.FilterAlbum(1, Blacklist(skipFiles: false));

        _releaseService.DidNotReceive().DeleteMany(Arg.Any<List<AlbumRelease>>());
        _trackService.DidNotReceive().DeleteMany(Arg.Any<List<Track>>());
        Assert.Equal(0, result.ReleasesDeleted);
        Assert.Equal(1, result.ReleasesSkippedWithFiles);
    }

    [Fact]
    public void PickPreferred_prefers_digital_then_cd()
    {
        var vinyl = Release(1, "Vinyl", trackCount: 20);
        var cd = Release(2, "CD", trackCount: 12);
        var digital = Release(3, "Digital Media", trackCount: 11);

        var preferred = ReleaseFilterService.PickPreferred(new[] { vinyl, cd, digital });

        Assert.Equal(digital.Id, preferred.Id);
    }

    [Fact]
    public void Does_not_call_file_delete_or_search_services()
    {
        var vinyl = Release(1, "Vinyl");
        var cd = Release(2, "CD", monitored: true);
        _releaseService.GetReleasesByAlbum(1).Returns(
            _ => new List<AlbumRelease> { vinyl, cd },
            _ => new List<AlbumRelease> { cd });
        _trackService.GetTracksByRelease(Arg.Any<int>()).Returns(new List<Track>());

        _subject.FilterAlbum(1, Blacklist());

        _albumService.DidNotReceiveWithAnyArgs().DeleteAlbum(default, default);
        _releaseService.Received().DeleteMany(Arg.Any<List<AlbumRelease>>());
    }
}
