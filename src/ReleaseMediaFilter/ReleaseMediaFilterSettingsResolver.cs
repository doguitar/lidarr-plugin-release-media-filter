using System;
using System.Linq;
using NLog;
using NzbDrone.Core.Notifications;

namespace NzbDrone.Core.Plugins;

public interface IReleaseMediaFilterSettingsResolver
{
    FilterOptions? Resolve();

    int ResolveScanIntervalMinutes();
}

public class ReleaseMediaFilterSettingsResolver : IReleaseMediaFilterSettingsResolver
{
    public const int DefaultScanIntervalMinutes = 1440;

    private readonly Lazy<INotificationFactory> _notificationFactory;
    private readonly Logger _logger;

    public ReleaseMediaFilterSettingsResolver(Lazy<INotificationFactory> notificationFactory, Logger logger)
    {
        _notificationFactory = notificationFactory ?? throw new ArgumentNullException(nameof(notificationFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public FilterOptions? Resolve()
    {
        return GetSettings()?.ToFilterOptions();
    }

    public int ResolveScanIntervalMinutes()
    {
        var settings = GetSettings();
        return settings?.ScanIntervalMinutes > 0 ? settings.ScanIntervalMinutes : DefaultScanIntervalMinutes;
    }

    private ReleaseMediaFilterSettings? GetSettings()
    {
        try
        {
            var matching = _notificationFactory.Value.All()
                .Where(definition => definition.Enable && definition.Settings is ReleaseMediaFilterSettings)
                .ToList();

            if (matching.Count == 0)
            {
                _logger.Debug("Release Media Filter: no Connect notification found, filtering disabled");
                return null;
            }

            if (matching.Count > 1)
            {
                _logger.Warn("Release Media Filter: multiple enabled connections found ({0}), filtering disabled", matching.Count);
                return null;
            }

            return (ReleaseMediaFilterSettings)matching[0].Settings;
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "Release Media Filter: failed to resolve Connect settings, filtering disabled");
            return null;
        }
    }
}
