using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Plugins;

public class ReleaseMediaFilterScanSettings : IProviderConfig
{
    public NzbDroneValidationResult Validate()
    {
        return new NzbDroneValidationResult();
    }
}
