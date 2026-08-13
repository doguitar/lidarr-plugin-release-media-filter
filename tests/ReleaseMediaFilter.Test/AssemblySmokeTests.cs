using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music.Events;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Plugins;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class AssemblySmokeTests
{
    [Fact]
    public void Assembly_exports_plugin_identity()
    {
        var assembly = typeof(ReleaseMediaFilterPlugin).Assembly;
        var pluginRoots = assembly.GetExportedTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IPlugin).IsAssignableFrom(type))
            .ToList();

        var pluginType = Assert.Single(pluginRoots);
        var plugin = Assert.IsType<ReleaseMediaFilterPlugin>(Activator.CreateInstance(pluginType));
        Assert.Equal("Release Media Filter", plugin.Name);
        Assert.Equal("seanseymour", plugin.Owner);
        Assert.Equal("Lidarr.Plugin.ReleaseMediaFilter", assembly.GetName().Name);
    }

    [Fact]
    public void Assembly_contains_notification_and_scan_command()
    {
        var assembly = typeof(ReleaseMediaFilterPlugin).Assembly;

        Assert.Contains(assembly.GetTypes(), type => typeof(NotificationBase<ReleaseMediaFilterSettings>).IsAssignableFrom(type) && !type.IsAbstract);
        Assert.Contains(assembly.GetTypes(), type => typeof(Command).IsAssignableFrom(type) && type.Name == nameof(ReleaseMediaFilterScanCommand));
        Assert.Contains(assembly.GetTypes(), type => typeof(IExecute<ReleaseMediaFilterScanCommand>).IsAssignableFrom(type) && !type.IsAbstract);
    }

    [Fact]
    public void Assembly_handles_refresh_events()
    {
        var handlerType = typeof(ReleaseMediaFilterRefreshHandler);

        Assert.Contains(handlerType.GetInterfaces(), iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IHandle<>) && iface.GetGenericArguments()[0] == typeof(AlbumUpdatedEvent));
        Assert.Contains(handlerType.GetInterfaces(), iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IHandle<>) && iface.GetGenericArguments()[0] == typeof(AlbumInfoRefreshedEvent));
    }
}
