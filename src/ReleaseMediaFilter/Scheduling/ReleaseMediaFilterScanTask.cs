using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Music;
using NzbDrone.Core.Plugins.Scheduling;

namespace NzbDrone.Core.Plugins;

public class ReleaseMediaFilterScanTask : ScheduledTaskBase<ReleaseMediaFilterScanSettings>, IExecute<ReleaseMediaFilterScanCommand>
{
    private readonly IReleaseFilterService _filterService;
    private readonly IArtistService _artistService;
    private readonly IReleaseMediaFilterSettingsResolver _settingsResolver;
    private readonly Logger _logger;

    public ReleaseMediaFilterScanTask(
        IReleaseFilterService filterService,
        IArtistService artistService,
        IReleaseMediaFilterSettingsResolver settingsResolver,
        Logger logger)
    {
        _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
        _artistService = artistService ?? throw new ArgumentNullException(nameof(artistService));
        _settingsResolver = settingsResolver ?? throw new ArgumentNullException(nameof(settingsResolver));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override string Name => "Release Media Filter Scan";

    public override Type CommandType => typeof(ReleaseMediaFilterScanCommand);

    public override int IntervalMinutes => Math.Max(60, _settingsResolver.ResolveScanIntervalMinutes());

    public override CommandPriority Priority => CommandPriority.Low;

    public void Execute(ReleaseMediaFilterScanCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        var options = _settingsResolver.Resolve();
        if (options == null)
        {
            command.ResultMessage = "Scan skipped: Release Media Filter is not configured in Connect";
            _logger.Info("Release Media Filter scan skipped: no Connect notification configured");
            return;
        }

        if (command.ArtistId.GetValueOrDefault() > 0)
        {
            ExecuteArtistScan(command, options);
            return;
        }

        _logger.Info("Release Media Filter scheduled scan starting");

        var artists = _artistService.GetAllArtists() ?? new List<Artist>();
        var monitored = artists.Where(artist => artist.Monitored).ToList();
        var scanned = 0;
        var failed = 0;
        var aggregate = FilterResult.Empty;

        foreach (var artist in monitored)
        {
            try
            {
                aggregate = aggregate.Add(_filterService.FilterArtist(artist, options));
                scanned++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.Warn(ex, "Release Media Filter scan failed for artist {0} '{1}'", artist.Id, artist.Name);
            }
        }

        command.ResultMessage =
            $"{scanned} artists scanned, {aggregate.ReleasesDeleted} releases deleted, {aggregate.ReleasesSkippedWithFiles} skipped (have files), {aggregate.MonitoredSwitched} switched" +
            (failed > 0 ? $", {failed} failed" : string.Empty);

        _logger.Info("Release Media Filter scan complete: {0}", command.ResultMessage);
    }

    private void ExecuteArtistScan(ReleaseMediaFilterScanCommand command, FilterOptions options)
    {
        var artistId = command.ArtistId.GetValueOrDefault();
        Artist artist;
        try
        {
            artist = _artistService.GetArtist(artistId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Release Media Filter per-artist scan failed: artist {0} not found", artistId);
            command.ResultMessage = $"Per-artist scan failed: artist {artistId} not found";
            return;
        }

        if (artist == null)
        {
            command.ResultMessage = $"Per-artist scan failed: artist {artistId} not found";
            return;
        }

        var result = _filterService.FilterArtist(artist, options);
        command.ResultMessage =
            $"1 artist scanned, {result.ReleasesDeleted} releases deleted, {result.ReleasesSkippedWithFiles} skipped (have files), {result.MonitoredSwitched} switched";
    }
}
