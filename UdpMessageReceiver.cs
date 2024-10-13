using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;

namespace OmniRigMQTT;

public class UdpMessageReceiver(int port, OmniRigInterface omniRigInterface)
{
    private readonly UdpClient _udpClient = new(port);

    public async Task StartListeningAsync()
    {
        while (true)
            try
            {
                var result = await _udpClient.ReceiveAsync();
                var message = Encoding.UTF8.GetString(result.Buffer);
                await ProcessMessageAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving UDP message: {ex.Message}");
            }
    }

    private async Task ProcessMessageAsync(string message)
    {
        try
        {
            // Remove any XML declaration if present
            message = RemoveXmlDeclaration(message);

            var doc = XDocument.Parse(message);
            if (doc.Root?.Name == "radio_setfrequency")
            {
                var frequencyString = doc.Root.Element("frequency")?.Value;
                if (double.TryParse(frequencyString, out var frequency))
                {
                    var frequencyHz = (long)(frequency * 1000); // Convert kHz to Hz
                    await omniRigInterface.SetVfoAFrequencyAsync(frequencyHz);
                    Console.WriteLine($"Set radio frequency to {frequency} kHz");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
            Console.WriteLine($"Received message: {message}");
        }
    }

    private string RemoveXmlDeclaration(string xml)
    {
        const string xmlDeclarationStart = "<?xml";
        const string xmlDeclarationEnd = "?>";

        var startIndex = xml.IndexOf(xmlDeclarationStart, StringComparison.OrdinalIgnoreCase);
        if (startIndex >= 0)
        {
            var endIndex = xml.IndexOf(xmlDeclarationEnd, startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex >= 0) return xml.Remove(startIndex, endIndex - startIndex + xmlDeclarationEnd.Length).Trim();
        }

        return xml;
    }
}
