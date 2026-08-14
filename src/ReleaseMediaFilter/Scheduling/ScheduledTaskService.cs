using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.ThingiProvider.Events;

namespace NzbDrone.Core.Plugins.Scheduling;

public class ScheduledTaskService :
    IHandle<ProviderAddedEvent<IMetadata>>,
    IHandle<ProviderUpdatedEvent<IMetadata>>,
    IHandle<ProviderDeletedEvent<IMetadata>>,
    IHandle<ProviderAddedEvent<INotification>>,
    IHandle<ProviderUpdatedEvent<INotification>>,
    IHandle<ProviderDeletedEvent<INotification>>
{
    private readonly IMetadataFactory _metadataFactory;
    private readonly IScheduledTaskRepository _scheduledTaskRepository;
    private readonly ICached<ScheduledTask> _cache;
    private readonly Logger _logger;
    private readonly HashSet<string> _registeredCommandTypes = new();

    public ScheduledTaskService(
        IMetadataFactory metadataFactory,
        IScheduledTaskRepository scheduledTaskRepository,
        ICacheManager cacheManager,
        Logger logger)
    {
        _metadataFactory = metadataFactory ?? throw new ArgumentNullException(nameof(metadataFactory));
        _scheduledTaskRepository = scheduledTaskRepository ?? throw new ArgumentNullException(nameof(scheduledTaskRepository));
        _cache = cacheManager.GetCache<ScheduledTask>(typeof(TaskManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Handle(ProviderAddedEvent<IMetadata> message)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var provider = _metadataFactory.GetInstance((MetadataDefinition)message.Definition);
        if (provider is IProvideScheduledTask scheduledTaskProvider)
        {
            RegisterTask(scheduledTaskProvider);
        }
    }

    public void Handle(ProviderUpdatedEvent<IMetadata> message)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var provider = _metadataFactory.GetInstance((MetadataDefinition)message.Definition);
        if (provider is IProvideScheduledTask scheduledTaskProvider)
        {
            UpdateTask(scheduledTaskProvider);
        }
    }

    public void Handle(ProviderDeletedEvent<IMetadata> message)
    {
        CleanupOrphanedTasks();
    }

    public void Handle(ProviderAddedEvent<INotification> message) => RefreshScanTaskIntervals();

    public void Handle(ProviderUpdatedEvent<INotification> message) => RefreshScanTaskIntervals();

    public void Handle(ProviderDeletedEvent<INotification> message) => RefreshScanTaskIntervals();

    public void RefreshScanTaskIntervals()
    {
        var providers = _metadataFactory.GetAvailableProviders()
            .OfType<IProvideScheduledTask>()
            .ToList();

        foreach (var provider in providers)
        {
            try
            {
                UpdateTask(provider);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to refresh scheduled task interval for {0}", provider.GetType().Name);
            }
        }
    }


    public void InitializeTasks()
    {
        var providers = _metadataFactory.GetAvailableProviders()
            .OfType<IProvideScheduledTask>()
            .ToList();

        foreach (var provider in providers)
        {
            try
            {
                RegisterTask(provider);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to register scheduled task for {0}", provider.GetType().Name);
            }
        }

        CleanupOrphanedTasks();
    }

    private void RegisterTask(IProvideScheduledTask provider)
    {
        var typeName = provider.CommandType.FullName!;
        var existingTask = FindScheduledTask(typeName);

        if (existingTask != null)
        {
            existingTask.Interval = provider.IntervalMinutes;
            existingTask.Priority = provider.Priority;

            if (existingTask.LastStartTime == default)
            {
                existingTask.LastStartTime = existingTask.LastExecution;
            }

            _scheduledTaskRepository.Update(existingTask);
            _cache.Set(typeName, existingTask);
            _registeredCommandTypes.Add(typeName);
            return;
        }

        var initialExecutionTime = DateTime.UtcNow.AddMinutes(-provider.IntervalMinutes - 1);
        var task = new ScheduledTask
        {
            TypeName = typeName,
            Interval = provider.IntervalMinutes,
            Priority = provider.Priority,
            LastExecution = initialExecutionTime,
            LastStartTime = initialExecutionTime
        };

        _scheduledTaskRepository.Insert(task);
        _cache.Set(typeName, task);
        _registeredCommandTypes.Add(typeName);
        _logger.Info(
            "Registered scheduled task: {0} interval={1}min priority={2}",
            typeName,
            provider.IntervalMinutes,
            provider.Priority);
    }

    private void UpdateTask(IProvideScheduledTask provider)
    {
        var typeName = provider.CommandType.FullName!;
        var existingTask = FindScheduledTask(typeName);

        if (existingTask == null)
        {
            RegisterTask(provider);
            return;
        }

        existingTask.Interval = provider.IntervalMinutes;
        existingTask.Priority = provider.Priority;

        if (existingTask.LastStartTime == default)
        {
            existingTask.LastStartTime = existingTask.LastExecution;
        }

        _scheduledTaskRepository.Update(existingTask);
        _cache.Set(typeName, existingTask);
        _registeredCommandTypes.Add(typeName);
    }

    private void CleanupOrphanedTasks()
    {
        var activeCommandTypes = _metadataFactory.GetAvailableProviders()
            .OfType<IProvideScheduledTask>()
            .Select(provider => provider.CommandType.FullName!)
            .ToHashSet();

        var orphaned = _registeredCommandTypes
            .Where(registered => !activeCommandTypes.Contains(registered))
            .ToList();

        foreach (var typeName in orphaned)
        {
            var task = FindScheduledTask(typeName);
            if (task != null)
            {
                _scheduledTaskRepository.Delete(task.Id);
                _cache.Remove(typeName);
            }

            _registeredCommandTypes.Remove(typeName);
        }
    }

    private ScheduledTask? FindScheduledTask(string typeName)
    {
        try
        {
            return _scheduledTaskRepository.All()
                .FirstOrDefault(task => task.TypeName == typeName);
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "Failed to query scheduled tasks for {0}", typeName);
            return null;
        }
    }
}
