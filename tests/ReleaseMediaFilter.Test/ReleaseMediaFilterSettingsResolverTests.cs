using NSubstitute;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Plugins;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class ReleaseMediaFilterSettingsResolverTests
{
    [Fact]
    public void Resolve_returns_null_when_no_connect_notification_exists()
    {
        var factory = Substitute.For<INotificationFactory>();
        factory.All().Returns(new List<NotificationDefinition>());
        var subject = new ReleaseMediaFilterSettingsResolver(
            new Lazy<INotificationFactory>(() => factory),
            NLog.LogManager.GetLogger("test"));

        Assert.Null(subject.Resolve());
    }

    [Fact]
    public void Resolve_returns_configured_options_when_connect_notification_exists()
    {
        var factory = Substitute.For<INotificationFactory>();
        factory.All().Returns(new List<NotificationDefinition>
        {
            new()
            {
                Settings = new ReleaseMediaFilterSettings
                {
                    FilterMode = FilterMode.Whitelist,
                    MediaTypes = "CD"
                }
            }
        });
        var subject = new ReleaseMediaFilterSettingsResolver(
            new Lazy<INotificationFactory>(() => factory),
            NLog.LogManager.GetLogger("test"));

        var options = subject.Resolve();

        Assert.NotNull(options);
        Assert.Equal(FilterMode.Whitelist, options!.Mode);
        Assert.Contains("CD", options.MediaTypes);
    }
}
