using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OmniRigMQTT;

public class UdpMessageSender : IDisposable
{
    private readonly IPEndPoint? _endPoint;
    private readonly UdpClient _udpClient;
    private DateTime _lastLogTime = DateTime.Now;
    private string _lastSentMessage = string.Empty;
    private DateTime _lastSentTime = DateTime.MinValue;
    private int _messagesSent;

    public UdpMessageSender(string? addressOrHostname, int port)
    {
        _udpClient = new UdpClient(AddressFamily.InterNetwork);
        _udpClient.DontFragment = true;

        try
        {
            if (!IPAddress.TryParse(addressOrHostname, out var ipAddress) && addressOrHostname != null)
            {
                // If it's not a valid IP address, assume it's a hostname and resolve it
                var addresses = Dns.GetHostAddresses(addressOrHostname)
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                    .ToArray();
                if (addresses.Length == 0)
                    throw new ArgumentException($"Unable to resolve hostname to an IPv4 address: {addressOrHostname}");
                ipAddress = addresses[0]; // Use the first resolved IPv4 address
                Console.WriteLine($"Resolved IP: {ipAddress}");
            }

            if (ipAddress != null) _endPoint = new IPEndPoint(ipAddress, port);
            Console.WriteLine($"UDP sender initialized with endpoint: {_endPoint}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing UdpMessageSender: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        _udpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task SendMessageAsync(string message)
    {
        var now = DateTime.Now;
        var timeSinceLastSent = now - _lastSentTime;

        if (message != _lastSentMessage || timeSinceLastSent.TotalSeconds >= 5)
        {
            var data = Encoding.UTF8.GetBytes(message);
            await _udpClient.SendAsync(data, data.Length, _endPoint);
            _messagesSent++;
            _lastSentMessage = message;
            _lastSentTime = now;
            // LogMessageSent(message);
        }
    }

    private void LogMessageSent(string message)
    {
        var now = DateTime.Now;
        if (!((now - _lastLogTime).TotalSeconds >= 5)) return;

        Console.Clear();
        Console.WriteLine($"Total messages sent: {_messagesSent}");
        Console.WriteLine($"Last message sent at: {now}");
        Console.WriteLine($"Last message content: {message}");
        _lastLogTime = now;
    }
}
