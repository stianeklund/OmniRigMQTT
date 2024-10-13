namespace OmniRigMQTT;

public class RadioInfo(
    long freq,
    long txFreq,
    string mode,
    bool isTransmitting,
    bool isSplit,
    bool isConnected,
    int activeRadioNr,
    string radioName)
{
    public long Freq { get; init; } = freq;
    public long TxFreq { get; init; } = txFreq;
    public string Mode { get; init; } = mode;
    public bool IsTransmitting { get; init; } = isTransmitting;

    // public int RadioNr { get; set; }
    public bool IsSplit { get; init; } = isSplit;

    public bool IsConnected { get; init; } = isConnected;

    public int ActiveRadioNr { get; init; } = activeRadioNr;

    public string RadioName { get; init; } = radioName;
    // Add other properties to match the XML structure

    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType()) return false;

        var other = (RadioInfo)obj;

        return Freq == other.Freq &&
               TxFreq == other.TxFreq &&
               Mode == other.Mode &&
               IsTransmitting == other.IsTransmitting &&
               IsSplit == other.IsSplit &&
               IsConnected == other.IsConnected &&
               ActiveRadioNr == other.ActiveRadioNr &&
               RadioName == other.RadioName;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Freq, TxFreq, Mode, IsTransmitting, IsSplit, IsConnected, ActiveRadioNr, RadioName);
    }
}
