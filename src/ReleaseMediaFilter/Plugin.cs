using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ReleaseMediaFilter.Test")]

namespace NzbDrone.Core.Plugins;

public sealed class ReleaseMediaFilterPlugin : Plugin
{
    public const string DisplayName = "Release Media Filter";
    public const string RepositoryOwner = "seanseymour";
    public const string RepositoryUrl = "https://github.com/seanseymour/lidarr-plugin-release-media-filter";

    public override string Name => DisplayName;

    public override string Owner => RepositoryOwner;

    public override string GithubUrl => RepositoryUrl;
}
