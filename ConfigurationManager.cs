using System.Text.Json;

namespace OmniRigMQTT;

public class ConfigurationManager
{
    private const string ConfigFileName = "config.json";

    public static Config LoadConfiguration()
    {
        if (!File.Exists(ConfigFileName))
        {
            var defaultConfig = new Config
            {
                UdpSender = new UdpConfig { Address = "localhost", Port = 12060 },
                UdpReceiverPort = 12060
            };
            SaveConfiguration(defaultConfig);
            return defaultConfig;
        }

        var jsonString = File.ReadAllText(ConfigFileName);
        return JsonSerializer.Deserialize<Config>(jsonString) ?? new Config();
    }

    public static void SaveConfiguration(Config config)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var jsonString = JsonSerializer.Serialize(config, options);
        File.WriteAllText(ConfigFileName, jsonString);
    }

    public class UdpConfig
    {
        public string? Address { get; set; }
        public int Port { get; set; }
    }

    public class Config
    {
        public UdpConfig UdpSender { get; init; } = new();
        public int UdpReceiverPort { get; set; }
        public MqttConfig MqttBroker { get; init; } = new();
    }

    public class MqttConfig
    {
        public string Address { get; set; } = "localhost";
        public int Port { get; set; } = 1884;
        public bool UseWebSockets { get; set; } = false;
    }
}
