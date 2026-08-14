using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.Plugins;

public enum FilterMode
{
    [FieldOption(Label = "Blacklist", Hint = "Delete releases whose media includes any listed type.")]
    Blacklist = 0,

    [FieldOption(Label = "Whitelist", Hint = "Keep only releases whose media is entirely in the listed types.")]
    Whitelist = 1
}

public enum NoAllowedReleaseAction
{
    [FieldOption(Label = "Delete filtered releases", Hint = "Remove vinyl/cassette even if no CD or digital alternative remains.")]
    DeleteFiltered = 0,

    [FieldOption(Label = "Keep as last resort", Hint = "Leave the filtered release in place when it is the only one.")]
    KeepLastResort = 1
}

public sealed class FilterOptions
{
    public FilterOptions(
        FilterMode mode,
        IEnumerable<string> mediaTypes,
        NoAllowedReleaseAction noAllowedReleaseAction,
        bool skipReleasesWithFiles)
    {
        Mode = mode;
        MediaTypes = (mediaTypes ?? Array.Empty<string>())
            .Select(t => t?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase)!;
        NoAllowedReleaseAction = noAllowedReleaseAction;
        SkipReleasesWithFiles = skipReleasesWithFiles;
    }

    public FilterMode Mode { get; }

    public IReadOnlySet<string> MediaTypes { get; }

    public NoAllowedReleaseAction NoAllowedReleaseAction { get; }

    public bool SkipReleasesWithFiles { get; }
}
