using NSubstitute;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Music;
using NzbDrone.Core.Plugins;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class ReleaseFilterServiceTests
{
    private readonly IReleaseService _releaseService = Substitute.For<IReleaseService>();
    private readonly ITrackService _trackService = Substitute.For<ITrackService>();
    private readonly IAlbumService _albumService = Substitute.For<IAlbumService>();
    private readonly IMediaFileService _mediaFileService = Substitute.For<IMediaFileService>();
    private readonly IDeleteMediaFiles _deleteMediaFiles = Substitute.For<IDeleteMediaFiles>();
    private readonly IManageCommandQueue _commandQueue = Substitute.For<IManageCommandQueue>();
    private readonly ReleaseFilterService _subject;

    public ReleaseFilterServiceTests()
    {
        _subject = new ReleaseFilterService(
            _releaseService,
            _trackService,
            _albumService,
            _mediaFileService,
            _deleteMediaFiles,
            _commandQueue,
            NLog.LogManager.GetLogger("test"));
    }

    private static FilterOptions Blacklist(
        NoAllowedReleaseAction fallback = NoAllowedReleaseAction.DeleteFiltered,
        bool skipFiles = true,
        bool searchAfter = false,
        IEnumerable<ReleaseSortRule>? sortRules = null) =>
        new(FilterMode.Blacklist, new[] { "Vinyl", "Cassette" }, fallback, skipFiles, searchAfter, sortRules);

    private static AlbumRelease Release(int id, string format, bool monitored = false, int trackCount = 10, string status = "Official", params string[] countries)
    {
        return new AlbumRelease
        {
            Id = id,
            AlbumId = 1,
            Title = $"Release {id}",
            Status = status,
            TrackCount = trackCount,
            Monitored = monitored,
            Country = countries.ToList(),
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
        _releaseService.GetReleasesByAlbum(1).Returns(
            _ => new List<AlbumRelease> { vinyl, cassette, digital },
            _ => new List<AlbumRelease> { digital });
        _trackService.GetTracksByRelease(Arg.Any<int>()).Returns(new List<Track>());

        var result = _subject.FilterAlbum(1, Blacklist());

        _releaseService.Received().DeleteMany(Arg.Is<List<AlbumRelease>>(list => list.Count == 2));
        _releaseService.Received(1).SetMonitored(digital);
        Assert.Equal(2, result.ReleasesDeleted);
        Assert.Equal(1, result.MonitoredSwitched);
        _deleteMediaFiles.DidNotReceiveWithAnyArgs().DeleteTrackFile(default!);
        _commandQueue.DidNotReceiveWithAnyArgs().Push(default(AlbumSearchCommand)!);
    }

    [Fact]
    public void Last_resort_keeps_sole_vinyl()
    {
        var vinyl = Release(1, "Vinyl", monitored: true);
        _releaseService.GetReleasesByAlbum(1).Returns(new List<AlbumRelease> { vinyl });
        _trackService.GetTracksByRelease(Arg.Any<int>()).Returns(new List<Track>());

        var result = _subject.FilterAlbum(1, Blacklist(NoAllowedReleaseAction.KeepLastResort));

        _releaseService.DidNotReceive().DeleteMany(Arg.Any<List<AlbumRelease>>());
        Assert.Equal(1, result.ReleasesKeptLastResort);
    }

    [Fact]
    public void Last_resort_keeps_one_vinyl_and_deletes_the_rest()
    {
        var vinylA = Release(1, "Vinyl", trackCount: 8);
        var vinylB = Release(2, "Vinyl", trackCount: 12, monitored: true);
        _releaseService.GetReleasesByAlbum(1).Returns(
            _ => new List<AlbumRelease> { vinylA, vinylB },
            _ => new List<AlbumRelease> { vinylB });
        _trackService.GetTracksByRelease(Arg.Any<int>()).Returns(new List<Track>());

        var result = _subject.FilterAlbum(1, Blacklist(NoAllowedReleaseAction.KeepLastResort));

        _releaseService.Received().DeleteMany(Arg.Is<List<AlbumRelease>>(list => list.Single().Id == 1));
        Assert.Equal(1, result.ReleasesDeleted);
        Assert.Equal(1, result.ReleasesKeptLastResort);
    }

    [Fact]
    public void Default_deletes_sole_vinyl_when_no_alternative()
    {
        var vinyl = Release(1, "Vinyl", monitored: true);
        _releaseService.GetReleasesByAlbum(1).Returns(
            _ => new List<AlbumRelease> { vinyl },
            _ => new List<AlbumRelease>());
        _trackService.GetTracksByRelease(Arg.Any<int>()).Returns(new List<Track>());

        var result = _subject.FilterAlbum(1, Blacklist());

        _releaseService.Received().DeleteMany(Arg.Is<List<AlbumRelease>>(list => list.Single().Id == 1));
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
        _deleteMediaFiles.DidNotReceiveWithAnyArgs().DeleteTrackFile(default!);
        _releaseService.Received(1).SetMonitored(cd);
        Assert.Equal(0, result.ReleasesDeleted);
        Assert.Equal(1, result.ReleasesSkippedWithFiles);
        Assert.Equal(1, result.MonitoredSwitched);
    }

    [Fact]
    public void Recycle_bin_deletes_files_when_skip_setting_is_off()
    {
        var vinyl = Release(1, "Vinyl", monitored: true);
        var cd = Release(2, "CD");
        var file = new TrackFile { Id = 50, Path = @"C:\music\vinyl.flac", AlbumId = 1 };
        _releaseService.GetReleasesByAlbum(1).Returns(
            _ => new List<AlbumRelease> { vinyl, cd },
            _ => new List<AlbumRelease> { cd });
        _trackService.GetTracksByRelease(1).Returns(new List<Track>
        {
            Track(10, 1, hasFile: true),
            Track(11, 1, hasFile: false)
        });
        _trackService.GetTracksByRelease(2).Returns(new List<Track>());
        _mediaFileService.GetFilesByRelease(1).Returns(new List<TrackFile> { file });

        var result = _subject.FilterAlbum(1, Blacklist(skipFiles: false));

        _deleteMediaFiles.Received(1).DeleteTrackFile(file);
        _releaseService.Received().DeleteMany(Arg.Is<List<AlbumRelease>>(list => list.Single().Id == 1));
        _trackService.Received().DeleteMany(Arg.Is<List<Track>>(list => list.Single().Id == 11));
        Assert.Equal(1, result.ReleasesDeleted);
        Assert.Equal(1, result.FilesDeleted);
        Assert.Equal(0, result.ReleasesSkippedWithFiles);
    }

    [Fact]
    public void Leaves_release_when_file_cleanup_fails()
    {
        var vinyl = Release(1, "Vinyl", monitored: true);
        var cd = Release(2, "CD", monitored: true);
        var file = new TrackFile { Id = 50, Path = @"C:\music\vinyl.flac", AlbumId = 1 };
        _releaseService.GetReleasesByAlbum(1).Returns(new List<AlbumRelease> { vinyl, cd });
        _trackService.GetTracksByRelease(1).Returns(new List<Track> { Track(10, 1, hasFile: true) });
        _trackService.GetTracksByRelease(2).Returns(new List<Track>());
        _mediaFileService.GetFilesByRelease(1).Returns(new List<TrackFile> { file });
        _deleteMediaFiles.When(d => d.DeleteTrackFile(file)).Do(_ => throw new InvalidOperationException("recycle failed"));

        var result = _subject.FilterAlbum(1, Blacklist(skipFiles: false));

        _releaseService.DidNotReceive().DeleteMany(Arg.Any<List<AlbumRelease>>());
        Assert.Equal(0, result.ReleasesDeleted);
        Assert.Equal(1, result.ReleasesSkippedWithFiles);
        Assert.Equal(0, result.FilesDeleted);
    }

    [Fact]
    public void Queues_album_search_after_file_cleanup_when_enabled()
    {
        var vinyl = Release(1, "Vinyl", monitored: true);
        var cd = Release(2, "CD");
        var file = new TrackFile { Id = 50, Path = @"C:\music\vinyl.flac", AlbumId = 1 };
        _releaseService.GetReleasesByAlbum(1).Returns(
            _ => new List<AlbumRelease> { vinyl, cd },
            _ => new List<AlbumRelease> { cd });
        _trackService.GetTracksByRelease(1).Returns(new List<Track> { Track(10, 1, hasFile: true) });
        _trackService.GetTracksByRelease(2).Returns(new List<Track>());
        _mediaFileService.GetFilesByRelease(1).Returns(new List<TrackFile> { file });

        var result = _subject.FilterAlbum(1, Blacklist(skipFiles: false, searchAfter: true));

        _commandQueue.Received(1).Push(Arg.Is<AlbumSearchCommand>(c => c.AlbumIds.Single() == 1));
        Assert.Equal(1, result.SearchesQueued);
        Assert.Equal(1, result.MonitoredSwitched);
    }

    [Fact]
    public async Task FilterAlbum_serializes_overlapping_calls_for_the_same_album()
    {
        var vinyl = Release(1, "Vinyl");
        var cd = Release(2, "CD", monitored: true);
        var inFlight = 0;
        var maxInFlight = 0;
        var started = new ManualResetEventSlim(false);

        _releaseService.GetReleasesByAlbum(1).Returns(_ =>
        {
            var current = Interlocked.Increment(ref inFlight);
            if (current > maxInFlight)
            {
                maxInFlight = current;
            }

            if (current == 1)
            {
                started.Set();
                Thread.Sleep(50);
            }

            Interlocked.Decrement(ref inFlight);
            return new List<AlbumRelease> { vinyl, cd };
        });
        _trackService.GetTracksByRelease(Arg.Any<int>()).Returns(new List<Track>());

        var first = Task.Run(() => _subject.FilterAlbum(1, Blacklist()));
        started.Wait(TimeSpan.FromSeconds(2));
        var second = Task.Run(() => _subject.FilterAlbum(1, Blacklist()));
        await Task.WhenAll(first, second);

        Assert.Equal(1, maxInFlight);
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
    public void Switches_monitored_release_using_configured_sort()
    {
        var longUs = Release(1, "CD", monitored: true, trackCount: 18, countries: "US");
        var shortGb = Release(2, "CD", trackCount: 10, countries: "GB");
        _releaseService.GetReleasesByAlbum(1).Returns(new List<AlbumRelease> { longUs, shortGb });
        _trackService.GetTracksByRelease(Arg.Any<int>()).Returns(new List<Track>());

        var options = Blacklist(sortRules: new[]
        {
            new ReleaseSortRule(ReleaseSortField.MediumRegex, ReleaseSortDirection.Descending, "^CD$"),
            new ReleaseSortRule(ReleaseSortField.TrackCount, ReleaseSortDirection.Ascending, null),
            new ReleaseSortRule(ReleaseSortField.CountryRegex, ReleaseSortDirection.Descending, "US|GB|UK")
        });

        var result = _subject.FilterAlbum(1, options);

        _releaseService.Received(1).SetMonitored(shortGb);
        Assert.Equal(1, result.MonitoredSwitched);
        Assert.Equal(0, result.ReleasesDeleted);
    }

    [Fact]
    public void Does_not_delete_album_when_filtering_releases()
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
