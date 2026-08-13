using System;
using NLog;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Plugins.Scheduling;

public class ScheduledTaskServiceStarter : IHandle<ApplicationStartedEvent>
{
    private readonly ScheduledTaskService _scheduledTaskService;
    private readonly Logger _logger;

    public ScheduledTaskServiceStarter(ScheduledTaskService scheduledTaskService, Logger logger)
    {
        _scheduledTaskService = scheduledTaskService ?? throw new ArgumentNullException(nameof(scheduledTaskService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Handle(ApplicationStartedEvent message)
    {
        _logger.Debug("ApplicationStarted: initializing Release Media Filter scheduled tasks");
        _scheduledTaskService.InitializeTasks();
    }
}
