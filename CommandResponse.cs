namespace OmniRigMQTT;

/// <summary>
///     Represents a response message sent back to a client.
/// </summary>
public class CommandResponse
{
    public string Status { get; set; } // "success" or "error"
    public object Result { get; set; } // Present if status is "success"
    public string ErrorCode { get; set; } // Present if status is "error"
    public string ErrorMessage { get; set; } // Present if status is "error"
    public string Timestamp { get; set; }
    public string CorrelationData { get; set; }
}
