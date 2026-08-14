using System.Collections.Generic;

namespace NzbDrone.Core.Plugins;

public sealed class FilterResult
{
    public static FilterResult Empty { get; } = new();

    public int ReleasesInspected { get; init; }

    public int ReleasesDeleted { get; init; }

    public int ReleasesSkippedWithFiles { get; init; }

    public int ReleasesKeptLastResort { get; init; }

    public int MonitoredSwitched { get; init; }

    public int FilesDeleted { get; init; }

    public int SearchesQueued { get; init; }

    public FilterResult Add(FilterResult other)
    {
        if (other == null)
        {
            return this;
        }

        return new FilterResult
        {
            ReleasesInspected = ReleasesInspected + other.ReleasesInspected,
            ReleasesDeleted = ReleasesDeleted + other.ReleasesDeleted,
            ReleasesSkippedWithFiles = ReleasesSkippedWithFiles + other.ReleasesSkippedWithFiles,
            ReleasesKeptLastResort = ReleasesKeptLastResort + other.ReleasesKeptLastResort,
            MonitoredSwitched = MonitoredSwitched + other.MonitoredSwitched,
            FilesDeleted = FilesDeleted + other.FilesDeleted,
            SearchesQueued = SearchesQueued + other.SearchesQueued
        };
    }
}
