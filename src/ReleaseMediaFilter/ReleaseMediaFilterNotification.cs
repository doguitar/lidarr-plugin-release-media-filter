using System;
using FluentValidation.Results;
using NLog;
using NzbDrone.Core.Notifications;

namespace NzbDrone.Core.Plugins;

public class ReleaseMediaFilterNotification : NotificationBase<ReleaseMediaFilterSettings>
{
    private readonly IReleaseFilterService _filterService;
    private readonly Logger _logger;

    public ReleaseMediaFilterNotification(IReleaseFilterService filterService, Logger logger)
    {
        _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override string Name => ReleaseMediaFilterPlugin.DisplayName;

    public override string Link => ReleaseMediaFilterPlugin.RepositoryUrl;

    public override ValidationResult Test()
    {
        return new ValidationResult();
    }

    public override void OnReleaseImport(AlbumDownloadMessage message)
    {
        try
        {
            var album = message?.Album;
            if (album == null || album.Id <= 0)
            {
                return;
            }

            var options = (Definition?.Settings as ReleaseMediaFilterSettings ?? new ReleaseMediaFilterSettings()).ToFilterOptions();
            _filterService.FilterAlbum(album.Id, options);
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "Release Media Filter: import handler failed");
        }
    }
}
