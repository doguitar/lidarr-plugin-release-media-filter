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
                OnReleaseImport = true,
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

    [Fact]
    public void Resolve_returns_null_when_connect_notification_is_disabled()
    {
        var factory = Substitute.For<INotificationFactory>();
        factory.All().Returns(new List<NotificationDefinition>
        {
            new()
            {
                Settings = new ReleaseMediaFilterSettings { MediaTypes = "Vinyl" }
            }
        });
        var subject = new ReleaseMediaFilterSettingsResolver(
            new Lazy<INotificationFactory>(() => factory),
            NLog.LogManager.GetLogger("test"));

        Assert.Null(subject.Resolve());
    }

    [Fact]
    public void Resolve_ignores_disabled_definitions_when_an_enabled_one_exists()
    {
        var factory = Substitute.For<INotificationFactory>();
        factory.All().Returns(new List<NotificationDefinition>
        {
            new()
            {
                Settings = new ReleaseMediaFilterSettings { MediaTypes = "Cassette" }
            },
            new()
            {
                OnReleaseImport = true,
                Settings = new ReleaseMediaFilterSettings { MediaTypes = "CD" }
            }
        });
        var subject = new ReleaseMediaFilterSettingsResolver(
            new Lazy<INotificationFactory>(() => factory),
            NLog.LogManager.GetLogger("test"));

        var options = subject.Resolve();

        Assert.NotNull(options);
        Assert.Contains("CD", options!.MediaTypes);
        Assert.DoesNotContain("Cassette", options.MediaTypes);
    }
}
