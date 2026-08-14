using System;
using FluentValidation.Results;
using NLog;
using NzbDrone.Core.Notifications;

namespace NzbDrone.Core.Plugins;

public class ReleaseMediaFilterNotification : NotificationBase<ReleaseMediaFilterSettings>
{
    private readonly IReleaseFilterService _filterService;
    private readonly IReleaseMediaFilterSettingsResolver _settingsResolver;
    private readonly Logger _logger;

    public ReleaseMediaFilterNotification(
        IReleaseFilterService filterService,
        IReleaseMediaFilterSettingsResolver settingsResolver,
        Logger logger)
    {
        _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
        _settingsResolver = settingsResolver ?? throw new ArgumentNullException(nameof(settingsResolver));
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

            var options = _settingsResolver.Resolve();
            if (options == null)
            {
                return;
            }

            _filterService.FilterAlbum(album.Id, options);
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "Release Media Filter: import handler failed");
        }
    }
}
