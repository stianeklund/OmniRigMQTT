using System.Xml.Linq;

namespace OmniRigMQTT;

public static class RadioInfoXmlGenerator
{
    public static string GenerateXml(RadioInfo radioInfo)
    {
        var xml = new XElement("RadioInfo",
            new XElement("app", "N1MM-Gen"),
            new XElement("StationName", "Test"),
            new XElement("RadioNr", "1"),
            new XElement("Freq", radioInfo.Freq),
            new XElement("TXFreq", radioInfo.TxFreq),
            new XElement("Mode", radioInfo.Mode),
            new XElement("OpCall", "LB1TI"),
            new XElement("IsRunning", "False"),
            new XElement("FocusEntry", "204626"),
            new XElement("EntryWindowHwnd", "275678"),
            new XElement("Antenna", "8"),
            new XElement("Rotors", ""),
            new XElement("FocusRadioNr", "1"),
            new XElement("IsStereo", "False"),
            new XElement("IsSplit", radioInfo.IsSplit),
            new XElement("ActiveRadioNr", radioInfo.ActiveRadioNr),
            new XElement("IsTransmitting", radioInfo.IsTransmitting),
            new XElement("FunctionKeyCaption", ""),
            new XElement("RadioName", ""),
            new XElement("AuxAntSelected", "-1"),
            new XElement("AuxAntSelectedName", ""),
            new XElement("IsConnected", radioInfo.IsConnected));

        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + xml;
    }
}