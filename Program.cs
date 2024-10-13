using System.Runtime.InteropServices;

namespace OmniRigMQTT;

public class Program
{
    private static readonly OmniRigInterface OmniRigInterface = new();
    private static OmniRigMqttController? _omniRigMqtt;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting N1MM UDP Message Generator and Receiver with MQTT Support");

        var config = ConfigurationManager.LoadConfiguration();

        try
        {
            // Initialize OmniRigMqttController
            _omniRigMqtt = new OmniRigMqttController(config.MqttBroker.Address, config.MqttBroker.Port,
                config.MqttBroker.UseWebSockets);

            // Wait for the MQTT client to connect
            await _omniRigMqtt.WaitForConnectionAsync(TimeSpan.FromSeconds(30));

            if (args.Length == 3) HandleCommandLineArguments(args, config);

            var udpMessageSender = new UdpMessageSender(config.UdpSender.Address, config.UdpSender.Port);
            var udpMessageReceiver = new UdpMessageReceiver(config.UdpReceiverPort, OmniRigInterface);

            // Start the UDP receiver in a separate task
            _ = Task.Run(() => udpMessageReceiver.StartListeningAsync());

            // Start periodic status updates
            _ = Task.Run(() =>
                _omniRigMqtt.PublishRadioInfoPeriodically(TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(2)));

            Console.WriteLine("Starting OmniRigMQTT..");
            Console.WriteLine("Starting N1MM UDP message generation & receiver");
            Console.WriteLine("Press Ctrl+C to exit.");

            while (true)
            {
                try
                {
                    var radioInfo = await OmniRigInterface.GetRadioInfoAsyncRig1();
                    var xmlMessage = RadioInfoXmlGenerator.GenerateXml(radioInfo);

                    await udpMessageSender.SendMessageAsync(xmlMessage);
                    // MQTT publishing is handled by the periodic task, so we don't need to publish here
                }
                catch (Exception ex)
                {
                    await HandleExceptionAsync(ex);
                }

                await Task.Delay(100); // Wait for 100 milliseconds before checking for updates
            }
        }
        catch (Exception ex)
        {

        }
    }
    private static void HandleCommandLineArguments(string[] args, ConfigurationManager.Config config)
    {
        switch (args[0])
        {
            case "--set-sender-address":
                config.UdpSender.Address = args[1];
                config.UdpSender.Port = int.Parse(args[2]);
                ConfigurationManager.SaveConfiguration(config);
                Console.WriteLine($"Updated sender address to: {args[1]} and port to: {args[2]}");
                break;
            case "--set-receiver-port":
                config.UdpReceiverPort = int.Parse(args[1]);
                ConfigurationManager.SaveConfiguration(config);
                Console.WriteLine($"Updated receiver port to: {args[1]}");
                break; 
            case "--set-broker-address":
                config.UdpSender.Address = args[1];
                config.UdpSender.Port = int.Parse(args[2]);
                ConfigurationManager.SaveConfiguration(config);
                Console.WriteLine($"Updated sender address to: {args[1]} and port to: {args[2]}");
                break;
            case "--set-broker-port":
                config.MqttBroker.Address = args[1];
                config.MqttBroker.Port = int.Parse(args[2]);
                ConfigurationManager.SaveConfiguration(config);
                Console.WriteLine($"Updated sender address to: {args[1]} and port to: {args[2]}");
                break;
        }
    }

    private static async Task HandleExceptionAsync(Exception ex)
    {
        Console.WriteLine($"Error in main loop: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            Console.WriteLine($"Inner exception stack trace: {ex.InnerException.StackTrace}");
        }

        if (_omniRigMqtt != null) await _omniRigMqtt.PublishEventNotificationAsync("1", "error", ex.Message);
        await Task.Delay(5000); // Add a longer delay here to prevent rapid error logging
    }

    ~Program()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Marshal.ReleaseComObject(OmniRigInterface);
        _omniRigMqtt?.Dispose();
    }
}
