using NSubstitute;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Plugins;
using NzbDrone.Core.Plugins.Scheduling;
using NzbDrone.Core.ThingiProvider.Events;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class ScheduledTaskServiceTests
{
    [Fact]
    public void Connect_update_refreshes_scan_interval_from_provider()
    {
        var metadataFactory = Substitute.For<IMetadataFactory>();
        var repository = Substitute.For<IScheduledTaskRepository>();
        var cacheManager = Substitute.For<ICacheManager>();
        var cache = Substitute.For<ICached<ScheduledTask>>();
        cacheManager.GetCache<ScheduledTask>(typeof(TaskManager)).Returns(cache);

        var provider = Substitute.For<IMetadata, IProvideScheduledTask>();
        var scheduled = (IProvideScheduledTask)provider;
        scheduled.CommandType.Returns(typeof(ReleaseMediaFilterScanCommand));
        scheduled.IntervalMinutes.Returns(90);
        scheduled.Priority.Returns(NzbDrone.Core.Messaging.Commands.CommandPriority.Low);
        metadataFactory.GetAvailableProviders().Returns(new List<IMetadata> { provider });

        var existing = new ScheduledTask
        {
            Id = 7,
            TypeName = typeof(ReleaseMediaFilterScanCommand).FullName!,
            Interval = 1440
        };
        repository.All().Returns(new List<ScheduledTask> { existing });

        var subject = new ScheduledTaskService(
            metadataFactory,
            repository,
            cacheManager,
            NLog.LogManager.GetLogger("test"));

        subject.Handle(new ProviderUpdatedEvent<INotification>(new NotificationDefinition { OnReleaseImport = true }));

        repository.Received().Update(Arg.Is<ScheduledTask>(task => task.Id == 7 && task.Interval == 90));
    }

    [Fact]
    public void Registering_a_new_task_does_not_backdate_last_execution()
    {
        var metadataFactory = Substitute.For<IMetadataFactory>();
        var repository = Substitute.For<IScheduledTaskRepository>();
        var cacheManager = Substitute.For<ICacheManager>();
        var cache = Substitute.For<ICached<ScheduledTask>>();
        cacheManager.GetCache<ScheduledTask>(typeof(TaskManager)).Returns(cache);

        var provider = Substitute.For<IMetadata, IProvideScheduledTask>();
        var scheduled = (IProvideScheduledTask)provider;
        scheduled.CommandType.Returns(typeof(ReleaseMediaFilterScanCommand));
        scheduled.IntervalMinutes.Returns(1440);
        scheduled.Priority.Returns(NzbDrone.Core.Messaging.Commands.CommandPriority.Low);
        metadataFactory.GetAvailableProviders().Returns(new List<IMetadata> { provider });
        repository.All().Returns(new List<ScheduledTask>());

        var before = DateTime.UtcNow.AddSeconds(-2);
        var subject = new ScheduledTaskService(
            metadataFactory,
            repository,
            cacheManager,
            NLog.LogManager.GetLogger("test"));

        subject.InitializeTasks();
        var after = DateTime.UtcNow.AddSeconds(2);

        repository.Received().Insert(Arg.Is<ScheduledTask>(task =>
            task.LastExecution >= before && task.LastExecution <= after));
    }
}
