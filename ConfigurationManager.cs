using System.Text.Json;

namespace OmniRigMQTT;

public abstract class ConfigurationManager
{
    private const string ConfigFileName = "config.json";

    public static Config LoadConfiguration()
    {
        if (!File.Exists(ConfigFileName))
        {
            var defaultConfig = new Config
            {
                UdpSender = new UdpConfig { Address = "localhost", Port = 12060 },
                UdpReceiverPort = 12060,
                MqttBroker = new MqttConfig
                {
                    Address = "localhost",
                    Port = 1883,
                    UseWebSockets = false,
                    ConnectionName = "Connection Name",
                    Username = "",
                    Password = ""
                }
            };
            SaveConfiguration(defaultConfig);
            return defaultConfig;
        }

        var jsonString = File.ReadAllText(ConfigFileName);
        var config = JsonSerializer.Deserialize<Config>(jsonString) ?? new Config();

        // Ensure all properties are present
        if (string.IsNullOrEmpty(config.UdpSender.Address))
            config.UdpSender = new UdpConfig { Address = "localhost", Port = 12060 };
        if (string.IsNullOrEmpty(config.MqttBroker.Address))
            config.MqttBroker.Address = "localhost";
        if (config.MqttBroker.Port == 0)
            config.MqttBroker.Port = 1883;
        if (string.IsNullOrEmpty(config.MqttBroker.ConnectionName))
            config.MqttBroker.ConnectionName = "Connection Name";
        config.MqttBroker.Username ??= string.Empty;
        config.MqttBroker.Password ??= string.Empty;

        SaveConfiguration(config);

        return config;
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
        public UdpConfig UdpSender { get; set; } = new();
        public int UdpReceiverPort { get; set; }
        public MqttConfig MqttBroker { get; init; } = new();
    }

    public class MqttConfig
    {
        public string Address { get; set; } = "localhost";
        public int Port { get; set; } = 1883;
        public bool UseWebSockets { get; set; } = false;
        public string? ConnectionName { get; set; } = "Connection Name";
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
