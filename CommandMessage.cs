using System.Text.Json;

namespace OmniRigMQTT;

/// <summary>
///     Represents a command message received from a client.
/// </summary>
public class CommandMessage
{
    public string Command { get; set; }
    public Dictionary<string, JsonElement> Parameters { get; set; }
    public string Timestamp { get; set; }
    public string CorrelationData { get; set; }
}
