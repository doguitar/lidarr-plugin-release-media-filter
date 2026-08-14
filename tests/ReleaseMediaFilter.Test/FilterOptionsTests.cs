using NzbDrone.Core.Plugins;
using Xunit;

namespace ReleaseMediaFilter.Test;

public class FilterOptionsTests
{
    [Fact]
    public void MediaTypes_cannot_be_mutated_after_construction()
    {
        var options = new FilterOptions(FilterMode.Blacklist, new[] { "Vinyl" }, NoAllowedReleaseAction.KeepLastResort, true);

        Assert.Contains("Vinyl", options.MediaTypes);
        Assert.ThrowsAny<NotSupportedException>(() =>
        {
            if (options.MediaTypes is ICollection<string> mutable)
            {
                mutable.Add("Cassette");
            }
            else
            {
                throw new NotSupportedException();
            }
        });
    }
}
