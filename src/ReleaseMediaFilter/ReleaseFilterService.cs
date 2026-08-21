using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

public interface IReleaseFilterService
{
    FilterResult FilterAlbum(int albumId, FilterOptions options);

    FilterResult FilterArtist(Artist artist, FilterOptions options);
}

public class ReleaseFilterService : IReleaseFilterService
{
    private static readonly ConcurrentDictionary<int, object> AlbumGates = new();

    private readonly IReleaseService _releaseService;
    private readonly ITrackService _trackService;
    private readonly IAlbumService _albumService;
    private readonly IMediaFileService _mediaFileService;
    private readonly IDeleteMediaFiles _deleteMediaFiles;
    private readonly IManageCommandQueue _commandQueue;
    private readonly Logger _logger;

    public ReleaseFilterService(
        IReleaseService releaseService,
        ITrackService trackService,
        IAlbumService albumService,
        IMediaFileService mediaFileService,
        IDeleteMediaFiles deleteMediaFiles,
        IManageCommandQueue commandQueue,
        Logger logger)
    {
        _releaseService = releaseService ?? throw new ArgumentNullException(nameof(releaseService));
        _trackService = trackService ?? throw new ArgumentNullException(nameof(trackService));
        _albumService = albumService ?? throw new ArgumentNullException(nameof(albumService));
        _mediaFileService = mediaFileService ?? throw new ArgumentNullException(nameof(mediaFileService));
        _deleteMediaFiles = deleteMediaFiles ?? throw new ArgumentNullException(nameof(deleteMediaFiles));
        _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public FilterResult FilterArtist(Artist artist, FilterOptions options)
    {
        if (artist == null)
        {
            throw new ArgumentNullException(nameof(artist));
        }

        var albums = _albumService.GetAlbumsByArtist(artist.Id) ?? new List<Album>();
        var aggregate = FilterResult.Empty;

        foreach (var album in albums)
        {
            aggregate = aggregate.Add(FilterAlbum(album.Id, options));
        }

        return aggregate;
    }

    public FilterResult FilterAlbum(int albumId, FilterOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (albumId <= 0)
        {
            return FilterResult.Empty;
        }

        var gate = AlbumGates.GetOrAdd(albumId, _ => new object());
        lock (gate)
        {
            return FilterAlbumCore(albumId, options);
        }
    }

    private FilterResult FilterAlbumCore(int albumId, FilterOptions options)
    {
        var releases = _releaseService.GetReleasesByAlbum(albumId) ?? new List<AlbumRelease>();
        if (releases.Count == 0)
        {
            return FilterResult.Empty;
        }

        var filtered = releases.Where(release => MediaTypeMatcher.IsReleaseFiltered(release, options)).ToList();
        var allowed = releases.Except(filtered).ToList();

        if (filtered.Count == 0)
        {
            return new FilterResult
            {
                ReleasesInspected = releases.Count,
                MonitoredSwitched = SwitchMonitored(albumId, allowed, allowed, options)
            };
        }

        var keptLastResort = new List<AlbumRelease>();
        var deleteCandidates = filtered;

        if (allowed.Count == 0 && options.NoAllowedReleaseAction == NoAllowedReleaseAction.KeepLastResort)
        {
            var keep = PickPreferred(filtered, options);
            keptLastResort.Add(keep);
            deleteCandidates = filtered.Where(release => release.Id != keep.Id).ToList();
            _logger.Info(
                "Release Media Filter: keeping filtered release as last resort. albumId={0} release='{1}' extraFiltered={2}",
                albumId,
                keep.Title,
                deleteCandidates.Count);
        }

        var skippedWithFiles = new List<AlbumRelease>();
        var toDelete = new List<AlbumRelease>();
        var filesDeleted = 0;

        foreach (var release in deleteCandidates)
        {
            if (!HasImportedFiles(release))
            {
                toDelete.Add(release);
                continue;
            }

            if (options.SkipReleasesWithFiles)
            {
                skippedWithFiles.Add(release);
                continue;
            }

            if (!TryDeleteImportedFiles(albumId, release, out var deletedCount))
            {
                skippedWithFiles.Add(release);
                continue;
            }

            filesDeleted += deletedCount;
            toDelete.Add(release);
        }

        if (toDelete.Count > 0)
        {
            var tracks = toDelete
                .SelectMany(release => _trackService.GetTracksByRelease(release.Id) ?? new List<Track>())
                .Where(track => track != null && !track.HasFile)
                .ToList();

            if (tracks.Count > 0)
            {
                _trackService.DeleteMany(tracks);
            }

            _releaseService.DeleteMany(toDelete);

            _logger.Info(
                "Release Media Filter: deleted {0} filtered album release(s). albumId={1} titles={2}",
                toDelete.Count,
                albumId,
                string.Join(", ", toDelete.Select(release => $"{release.Title} [{string.Join('+', MediaTypeMatcher.GetFormats(release))}]")));
        }

        var remaining = _releaseService.GetReleasesByAlbum(albumId) ?? new List<AlbumRelease>();
        var remainingAllowed = remaining
            .Where(release => !MediaTypeMatcher.IsReleaseFiltered(release, options))
            .ToList();

        var switched = SwitchMonitored(albumId, remaining, remainingAllowed, options);

        var searchesQueued = 0;
        if (options.SearchAfterFileCleanup && filesDeleted > 0 && remainingAllowed.Count > 0)
        {
            try
            {
                _commandQueue.Push(new AlbumSearchCommand(new List<int> { albumId }));
                searchesQueued = 1;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Release Media Filter: failed to queue album search after file cleanup. albumId={0}", albumId);
            }
        }

        return new FilterResult
        {
            ReleasesInspected = releases.Count,
            ReleasesDeleted = toDelete.Count,
            ReleasesSkippedWithFiles = skippedWithFiles.Count,
            ReleasesKeptLastResort = keptLastResort.Count,
            MonitoredSwitched = switched,
            FilesDeleted = filesDeleted,
            SearchesQueued = searchesQueued
        };
    }

    private int SwitchMonitored(
        int albumId,
        IReadOnlyList<AlbumRelease> remaining,
        IReadOnlyList<AlbumRelease> remainingAllowed,
        FilterOptions options)
    {
        if (remainingAllowed.Count == 0)
        {
            return 0;
        }

        if (remaining.Any(HasImportedFiles))
        {
            _logger.Debug(
                "Release Media Filter: leaving monitored release unchanged because the album already has files. albumId={0}",
                albumId);
            return 0;
        }

        var ranked = ReleasePreference.Rank(remainingAllowed, options);
        var preferred = ranked[0];
        if (preferred.Monitored)
        {
            return 0;
        }

        _releaseService.SetMonitored(preferred);
        _logger.Info(
            "Release Media Filter: monitored first remaining release. albumId={0} release='{1}' formats={2} ranking={3}",
            albumId,
            preferred.Title,
            string.Join('+', MediaTypeMatcher.GetFormats(preferred)),
            string.Join(" | ", ranked.Select(release => release.Title)));
        return 1;
    }

    private bool TryDeleteImportedFiles(int albumId, AlbumRelease release, out int filesDeleted)
    {
        filesDeleted = 0;
        var files = _mediaFileService.GetFilesByRelease(release.Id) ?? new List<TrackFile>();
        if (files.Count == 0)
        {
            _logger.Warn(
                "Release Media Filter: tracks report files but none were found; skipping release. albumId={0} releaseId={1}",
                albumId,
                release.Id);
            return false;
        }

        try
        {
            foreach (var file in files)
            {
                _deleteMediaFiles.DeleteTrackFile(file);
                filesDeleted++;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Release Media Filter: file cleanup failed; leaving release in place. albumId={0} releaseId={1}",
                albumId,
                release.Id);
            filesDeleted = 0;
            return false;
        }
    }

    private bool HasImportedFiles(AlbumRelease release)
    {
        var tracks = _trackService.GetTracksByRelease(release.Id) ?? new List<Track>();
        return tracks.Any(track => track.HasFile);
    }

    internal static AlbumRelease PickPreferred(IReadOnlyList<AlbumRelease> releases, FilterOptions? options = null)
    {
        return ReleasePreference.Pick(releases, options);
    }
}
