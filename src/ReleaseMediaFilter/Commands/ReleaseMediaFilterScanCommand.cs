using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Plugins;

public class ReleaseMediaFilterScanCommand : Command
{
    private string _resultMessage = string.Empty;

    public ReleaseMediaFilterScanCommand()
    {
        SendUpdatesToClient = true;
    }

    public int? ArtistId { get; set; }

    public string ResultMessage
    {
        get => _resultMessage;
        set => _resultMessage = value ?? string.Empty;
    }

    public override string CompletionMessage => _resultMessage;
}
