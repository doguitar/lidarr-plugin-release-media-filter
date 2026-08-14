using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Events;

namespace NzbDrone.Core.Plugins;

public class ReleaseMediaFilterRefreshHandler :
    IHandle<AlbumUpdatedEvent>,
    IHandle<AlbumInfoRefreshedEvent>
{
    private readonly IReleaseFilterService _filterService;
    private readonly IReleaseMediaFilterSettingsResolver _settingsResolver;
    private readonly Logger _logger;
    private readonly HashSet<int> _inProgress = new();
    private readonly object _gate = new();

    public ReleaseMediaFilterRefreshHandler(
        IReleaseFilterService filterService,
        IReleaseMediaFilterSettingsResolver settingsResolver,
        Logger logger)
    {
        _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
        _settingsResolver = settingsResolver ?? throw new ArgumentNullException(nameof(settingsResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Handle(AlbumUpdatedEvent message)
    {
        if (message?.Album == null)
        {
            return;
        }

        FilterAlbumSafe(message.Album, "AlbumUpdatedEvent");
    }

    public void Handle(AlbumInfoRefreshedEvent message)
    {
        if (message == null)
        {
            return;
        }

        foreach (var album in Enumerate(message.Added))
        {
            FilterAlbumSafe(album, "AlbumInfoRefreshedEvent.Added");
        }

        foreach (var album in Enumerate(message.Updated))
        {
            FilterAlbumSafe(album, "AlbumInfoRefreshedEvent.Updated");
        }
    }

    private static IEnumerable<Album> Enumerate(IEnumerable<Album>? albums)
    {
        if (albums == null)
        {
            yield break;
        }

        foreach (var album in albums)
        {
            if (album != null)
            {
                yield return album;
            }
        }
    }

    private void FilterAlbumSafe(Album album, string source)
    {
        if (album.Id <= 0)
        {
            return;
        }

        lock (_gate)
        {
            if (!_inProgress.Add(album.Id))
            {
                return;
            }
        }

        try
        {
            var options = _settingsResolver.Resolve();
            if (options == null)
            {
                return;
            }

            var result = _filterService.FilterAlbum(album.Id, options);
            if (result.ReleasesDeleted > 0 || result.MonitoredSwitched > 0 || result.ReleasesKeptLastResort > 0)
            {
                _logger.Info(
                    "Release Media Filter: {0} albumId={1} album='{2}' inspected={3} deleted={4} skippedWithFiles={5} lastResort={6} switched={7}",
                    source,
                    album.Id,
                    album.Title,
                    result.ReleasesInspected,
                    result.ReleasesDeleted,
                    result.ReleasesSkippedWithFiles,
                    result.ReleasesKeptLastResort,
                    result.MonitoredSwitched);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "Release Media Filter: refresh filter failed. source={0} albumId={1} album='{2}'", source, album.Id, album.Title);
        }
        finally
        {
            lock (_gate)
            {
                _inProgress.Remove(album.Id);
            }
        }
    }
}
