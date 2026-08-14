using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Music;

namespace NzbDrone.Core.Plugins;

public interface IReleaseFilterService
{
    FilterResult FilterAlbum(int albumId, FilterOptions options);

    FilterResult FilterArtist(Artist artist, FilterOptions options);
}

public class ReleaseFilterService : IReleaseFilterService
{
    private static readonly string[] PreferredFormatOrder =
    {
        "Digital Media",
        "CD"
    };

    private readonly IReleaseService _releaseService;
    private readonly ITrackService _trackService;
    private readonly IAlbumService _albumService;
    private readonly Logger _logger;

    public ReleaseFilterService(
        IReleaseService releaseService,
        ITrackService trackService,
        IAlbumService albumService,
        Logger logger)
    {
        _releaseService = releaseService ?? throw new ArgumentNullException(nameof(releaseService));
        _trackService = trackService ?? throw new ArgumentNullException(nameof(trackService));
        _albumService = albumService ?? throw new ArgumentNullException(nameof(albumService));
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

        var releases = _releaseService.GetReleasesByAlbum(albumId) ?? new List<AlbumRelease>();
        if (releases.Count == 0)
        {
            return FilterResult.Empty;
        }

        var filtered = releases.Where(release => MediaTypeMatcher.IsReleaseFiltered(release, options)).ToList();
        var allowed = releases.Except(filtered).ToList();

        if (filtered.Count == 0)
        {
            return new FilterResult { ReleasesInspected = releases.Count };
        }

        var keptLastResort = new List<AlbumRelease>();
        var deleteCandidates = filtered;

        if (allowed.Count == 0 && options.NoAllowedReleaseAction == NoAllowedReleaseAction.KeepLastResort)
        {
            var keep = PickPreferred(filtered);
            keptLastResort.Add(keep);
            deleteCandidates = filtered.Where(release => release.Id != keep.Id).ToList();
            _logger.Info(
                "Release Media Filter: keeping filtered release as last resort. albumId={0} release='{1}' extraFiltered={2}",
                albumId,
                keep.Title,
                deleteCandidates.Count);
        }

        var skippedWithFiles = deleteCandidates.Where(HasImportedFiles).ToList();
        var toDelete = deleteCandidates.Except(skippedWithFiles).ToList();

        if (!options.SkipReleasesWithFiles && skippedWithFiles.Count > 0)
        {
            _logger.Warn(
                "Release Media Filter: file-aware delete is not implemented; skipping {0} release(s) that have imported files. albumId={1}",
                skippedWithFiles.Count,
                albumId);
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

        var switched = 0;
        if (remainingAllowed.Count > 0 && remainingAllowed.TrueForAll(release => !release.Monitored))
        {
            var preferred = PickPreferred(remainingAllowed);
            _releaseService.SetMonitored(preferred);
            switched = 1;

            _logger.Info(
                "Release Media Filter: monitored remaining release. albumId={0} release='{1}' formats={2}",
                albumId,
                preferred.Title,
                string.Join('+', MediaTypeMatcher.GetFormats(preferred)));
        }

        return new FilterResult
        {
            ReleasesInspected = releases.Count,
            ReleasesDeleted = toDelete.Count,
            ReleasesSkippedWithFiles = skippedWithFiles.Count,
            ReleasesKeptLastResort = keptLastResort.Count,
            MonitoredSwitched = switched
        };
    }

    private bool HasImportedFiles(AlbumRelease release)
    {
        var tracks = _trackService.GetTracksByRelease(release.Id) ?? new List<Track>();
        return tracks.Any(track => track.HasFile);
    }

    internal static AlbumRelease PickPreferred(IReadOnlyList<AlbumRelease> releases)
    {
        return releases
            .OrderByDescending(Score)
            .ThenByDescending(release => string.Equals(release.Status, "Official", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(release => release.TrackCount)
            .ThenBy(release => release.Id)
            .First();
    }

    private static int Score(AlbumRelease release)
    {
        var formats = MediaTypeMatcher.GetFormats(release);
        for (var index = 0; index < PreferredFormatOrder.Length; index++)
        {
            var preferred = PreferredFormatOrder[index];
            if (formats.Any(format => MediaTypeMatcher.FormatMatches(format, preferred)))
            {
                return (PreferredFormatOrder.Length - index) * 100;
            }
        }

        return 0;
    }
}
