using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace OmniRigMQTT;

/// <summary>
///     Represents the MQTT interface for OmniRig.
/// </summary>
public class OmniRigMqttController : IDisposable
{
    private const string ClientId = "OmniRigClient";
    private const string TopicPrefix = "omnirig/";
    private const string FrequentTopic = TopicPrefix + "frequent/radio_info";
    private const string SporadicTopic = TopicPrefix + "sporadic/radio_info";
    private readonly IManagedMqttClient _client;
    private readonly OmniRigInterface _omniRigInterface;
    private RadioInfo _lastPublishedRadioInfo;
    private DateTime _lastSporadicUpdate = DateTime.MinValue;
    private readonly TaskCompletionSource<bool> _connectionTcs = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="OmniRigMqttController" /> class.
    /// </summary>
    /// <param name="brokerAddress">The MQTT broker address.</param>
    /// <param name="brokerPort">The MQTT broker port.</param>
    /// <param name="connectionName"></param>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <param name="useWebSockets">Whether to use WebSockets for the connection.</param>
    public OmniRigMqttController(string brokerAddress, int brokerPort,
        string? connectionName, string? username = null, string? password = null, bool useWebSockets = false)
    {
        _omniRigInterface = new OmniRigInterface();
        var factory = new MqttFactory();
        _client = factory.CreateManagedMqttClient();
        _lastPublishedRadioInfo = new RadioInfo(0, 0, string.Empty, false,
            false, false, 0, string.Empty);

        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(ClientId)
            .WithProtocolVersion(MqttProtocolVersion.V500);

        if (useWebSockets)
            clientOptionsBuilder.WithWebSocketServer($"ws://{brokerAddress}:{brokerPort}/mqtt");
        else
            clientOptionsBuilder.WithTcpServer(brokerAddress, brokerPort);

        // Add authentication if username and password are provided
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            clientOptionsBuilder.WithCredentials(username, password);
            Console.WriteLine("MQTT authentication enabled.");
        }
        else
        {
            Console.WriteLine("MQTT authentication not used (username or password not provided).");
        }

        var options = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(clientOptionsBuilder.Build())
            .Build();

        _client.ConnectedAsync += OnConnected;
        _client.DisconnectedAsync += OnDisconnected;
        _client.ApplicationMessageReceivedAsync += HandleReceivedApplicationMessage;

        _client.StartAsync(options);
    }

    public async Task WaitForConnectionAsync(TimeSpan timeout)
    {
        if (await Task.WhenAny(_connectionTcs.Task, Task.Delay(timeout)) != _connectionTcs.Task)
        {
            throw new TimeoutException("Failed to connect to MQTT broker within the specified timeout.");
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task OnConnected(MqttClientConnectedEventArgs e)
    {
        Console.WriteLine("Connected to MQTT broker.");

        try
        {
            // Subscribe to the commands topic for all rigs
            var topicFilter = new MqttTopicFilterBuilder()
                .WithTopic("omnirig/+/commands")
                .WithAtLeastOnceQoS()
                .Build();

            await _client.SubscribeAsync([topicFilter]);
            Console.WriteLine("Subscribed to topic: omnirig/+/commands");

            // Optionally, publish a connection status message
            await PublishEventNotificationAsync("system", "connection", "Connected to MQTT broker");

            // Set the TaskCompletionSource to indicate successful connection
            _connectionTcs.TrySetResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during MQTT connection setup: {ex.Message}");
            _connectionTcs.TrySetException(ex);
        }
    }

    private Task OnDisconnected(MqttClientDisconnectedEventArgs e)
    {
        Console.WriteLine("Disconnected from MQTT broker.");
        return Task.CompletedTask;
    }

    private async Task HandleReceivedApplicationMessage(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            Console.WriteLine($"Received message on topic {topic}: {payload}");

            // Parse the topic to extract the rig_id
            var topicParts = topic.Split('/');
            if (topicParts.Length >= 2)
            {
                var rigId = topicParts[1];

                // Parse the payload as JSON
                CommandMessage? commandMessage = null;
                try
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    commandMessage = JsonSerializer.Deserialize<CommandMessage>(payload, jsonOptions);
                    Console.WriteLine(
                        $"Deserialized command message: {JsonSerializer.Serialize(commandMessage, options: new JsonSerializerOptions { WriteIndented = true })}");
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"JSON deserialization error: {jsonEx.Message}");
                    Console.WriteLine($"Payload causing the error: {payload}");
                }

                // Extract Correlation Data
                var correlationData = string.Empty;
                if (e.ApplicationMessage.CorrelationData != null)
                    correlationData = Encoding.UTF8.GetString(e.ApplicationMessage.CorrelationData);

                if (commandMessage != null)
                {
                    commandMessage.CorrelationData = correlationData;

                    if (string.IsNullOrWhiteSpace(commandMessage.Command))
                    {
                        Console.WriteLine("Received command is null or empty.");
                        var errorResponse = new CommandResponse
                        {
                            Status = "error",
                            ErrorCode = "INVALID_COMMAND",
                            ErrorMessage = "Received command is null or empty",
                            Timestamp = DateTime.UtcNow.ToString("o"),
                            CorrelationData = correlationData
                        };
                        await PublishResponseAsync(rigId, errorResponse, e.ApplicationMessage);
                        return;
                    }

                    Console.WriteLine($"Processing command: {commandMessage.Command}");
                    Console.WriteLine(
                        $"Command parameters: {JsonSerializer.Serialize(commandMessage.Parameters, new JsonSerializerOptions { WriteIndented = true })}");

                    // Handle the command
                    var response = await HandleCommandAsync(rigId, commandMessage, e.ApplicationMessage);

                    // Publish the response
                    await PublishResponseAsync(rigId, response, e.ApplicationMessage);
                }
                else
                {
                    Console.WriteLine("Failed to deserialize command message.");
                    var errorResponse = new CommandResponse
                    {
                        Status = "error",
                        ErrorCode = "INVALID_MESSAGE",
                        ErrorMessage = "Failed to deserialize command message",
                        Timestamp = DateTime.UtcNow.ToString("o"),
                        CorrelationData = correlationData
                    };
                    await PublishResponseAsync(rigId, errorResponse, e.ApplicationMessage);
                }
            }
            else
            {
                Console.WriteLine("Invalid topic format.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling message: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            // Optionally, publish an error event or response
        }
    }

    // Command structure:
    // {
    //     "command": "command_name",
    //     "parameters": {
    //         "param1": value1,
    //         "param2": value2
    //     }
    // }
    //
    // Supported commands:
    // 1. set_frequency: Sets the VFO A frequency
    //    Parameters: { "frequency": long }
    // 2. set_mode: Sets the radio mode
    //    Parameters: { "mode": string }
    // 3. set_ptt: Sets the PTT state
    //    Parameters: { "ptt": boolean }
    // 4. get_status: Retrieves the current radio status
    //    Parameters: {} (empty object)

    private async Task<CommandResponse> HandleCommandAsync(string rigId, CommandMessage commandMessage,
        MqttApplicationMessage requestMessage)
    {
        Console.WriteLine($"Handling command for rig {rigId}: {commandMessage.Command}");
        Console.WriteLine(
            $"Command parameters: {JsonSerializer.Serialize(commandMessage.Parameters, new JsonSerializerOptions { WriteIndented = true })}");

        var response = new CommandResponse
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            CorrelationData = commandMessage.CorrelationData
        };

        try
        {
            if (string.IsNullOrWhiteSpace(commandMessage.Command))
            {
                throw new Exception("Command is null or empty.");
            }

            switch (commandMessage.Command.ToLower())
            {
                case "set_frequency":
                    if (commandMessage.Parameters.TryGetValue("frequency", out var freqElement))
                    {
                        var frequency = freqElement.GetInt64();
                        if (commandMessage.Parameters.TryGetValue("vfo", out var vfoElement))
                        {
                            var vfo = vfoElement.GetString()?.ToUpper();
                            switch (vfo)
                            {
                                case "A":
                                    await _omniRigInterface.SetVfoAFrequencyAsync(frequency);
                                    Console.WriteLine($"Setting VFO A frequency to {frequency} Hz for rig {rigId}");
                                    break;
                                case "B":
                                    await _omniRigInterface.SetVfoBFrequencyAsync(frequency);
                                    Console.WriteLine($"Setting VFO B frequency to {frequency} Hz for rig {rigId}");
                                    break;
                                default:
                                    throw new Exception($"Invalid VFO specified: {vfo}. Use 'A' or 'B'.");
                            }
                        }
                        else
                        {
                            // Default to VFO A if no VFO is specified
                            await _omniRigInterface.SetVfoAFrequencyAsync(frequency);
                            Console.WriteLine(
                                $"No VFO specified. Defaulting to VFO A. Setting frequency to {frequency} Hz for rig {rigId}");
                        }

                        response.Status = "success";
                        response.Result = new
                        {
                            frequency,
                            vfo = commandMessage.Parameters.TryGetValue("vfo", out var v) ? v.GetString() : "A"
                        };
                        Console.WriteLine(
                            $"Frequency set successfully. Response: {JsonSerializer.Serialize(response)}");
                    }
                    else
                    {
                        throw new Exception("Parameter 'frequency' is missing.");
                    }

                    break;

                case "set_mode":
                    if (commandMessage.Parameters.TryGetValue("mode", out var modeElement))
                    {
                        var mode = modeElement.GetString();
                        if (mode != null)
                        {
                            await _omniRigInterface.SetModeAsync(OmniRigInterface.ParseMode(mode));
                            Console.WriteLine($"Setting mode to {mode} for rig {rigId}");
                            response.Status = "success";
                            response.Result = new { mode };
                        }
                    }
                    else
                    {
                        throw new Exception("Parameter 'mode' is missing.");
                    }

                    break;

                case "set_ptt":
                    if (commandMessage.Parameters.TryGetValue("ptt", out var pttElement))
                    {
                        var ptt = pttElement.GetBoolean();
                        // TODO: Implement PTT setting in OmniRigInterface
                        Console.WriteLine($"Setting PTT to {ptt} for rig {rigId}");
                        response.Status = "success";
                        response.Result = new { ptt };
                    }
                    else
                    {
                        throw new Exception("Parameter 'ptt' is missing.");
                    }

                    break;

                case "get_status":
                    var radioInfo = await _omniRigInterface.GetRadioInfoAsyncRig1();
                    Console.WriteLine($"Getting status for rig {rigId}");
                    response.Status = "success";
                    response.Result = radioInfo;
                    break;

                default:
                    response.Status = "error";
                    response.ErrorCode = "INVALID_COMMAND";
                    response.ErrorMessage = $"Unknown command: {commandMessage.Command}";
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling command: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            response.Status = "error";
            response.ErrorCode = "COMMAND_FAILED";
            response.ErrorMessage = ex.Message;
        }

        Console.WriteLine(
            $"Command response: {JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })}");
        return response;
    }

    private async Task PublishResponseAsync(string rigId, CommandResponse response,
        MqttApplicationMessage requestMessage)
    {
        // Determine the response topic
        var responseTopic = requestMessage.ResponseTopic ?? $"omnirig/{rigId}/responses";

        var responsePayload = JsonSerializer.Serialize(response);

        // Prepare Correlation Data
        byte[] correlationDataBytes = [];
        if (!string.IsNullOrEmpty(response.CorrelationData))
            correlationDataBytes = Encoding.UTF8.GetBytes(response.CorrelationData);

        var responseMessage = new MqttApplicationMessageBuilder()
            .WithTopic(responseTopic)
            .WithPayload(responsePayload)
            .WithQualityOfServiceLevel(requestMessage.QualityOfServiceLevel)
            .WithCorrelationData(correlationDataBytes)
            .Build();

        await _client.EnqueueAsync(responseMessage);

        Console.WriteLine($"Published response to topic {responseTopic}: {responsePayload}");
    }

    /// <summary>
    ///     Publishes a status update for the specified rig.
    /// </summary>
    /// <param name="rigId">The rig identifier.</param>
    public async Task PublishStatusUpdateAsync(string rigId)
    {
        var status = await _omniRigInterface.GetRadioInfoAsyncRig1();

        var payload = JsonSerializer.Serialize(status);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic($"omnirig/{rigId}/status")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag()
            .Build();

        await _client.EnqueueAsync(message);

        Console.WriteLine($"Published status update for rig {rigId}: {payload}");
    }

    /// <summary>
    ///     Publishes an event notification for the specified rig.
    /// </summary>
    /// <param name="rigId">The rig identifier.</param>
    /// <param name="eventType">The event type.</param>
    /// <param name="message">The event message.</param>
    public async Task PublishEventNotificationAsync(string rigId, string eventType, string message)
    {
        var eventNotification = new
        {
            event_type = eventType,
            message,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        var payload = JsonSerializer.Serialize(eventNotification);

        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic($"omnirig/{rigId}/events")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.EnqueueAsync(mqttMessage);

        Console.WriteLine($"Published event notification for rig {rigId}: {payload}");
    }

    public async Task StopAsync()
    {
        if (_client.IsStarted) await _client.StopAsync();
    }

    /// <summary>
    ///     Publishes radio information periodically to both frequent and sporadic topics.
    /// </summary>
    /// <param name="frequentInterval">The interval for frequent updates.</param>
    /// <param name="sporadicInterval">The interval for sporadic updates.</param>
    public async Task PublishRadioInfoPeriodically(TimeSpan frequentInterval, TimeSpan sporadicInterval)
    {
        while (true)
        {
            try
            {
                var radioInfo = await _omniRigInterface.GetRadioInfoAsyncRig1();

                // Check if it's time for a sporadic update
                if (DateTime.UtcNow - _lastSporadicUpdate >= sporadicInterval)
                {
                    await PublishToTopic(SporadicTopic, radioInfo);
                    _lastSporadicUpdate = DateTime.UtcNow;
                }

                // limit how often we publish (but let sporadic updates pass through w/o regard to content)
                if (radioInfo.Equals(_lastPublishedRadioInfo)) return;

                // Publish to frequent topic
                await PublishToTopic(FrequentTopic, radioInfo);
                _lastPublishedRadioInfo = radioInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PublishRadioInfoPeriodically: {ex.Message}");
                await PublishEventNotificationAsync("1", "error", $"Failed to publish radio info: {ex.Message}");
            }

            await Task.Delay(frequentInterval);
        }
    }

    private async Task PublishToTopic(string topic, RadioInfo radioInfo)
    {
        var payload = JsonSerializer.Serialize(radioInfo);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag()
            .Build();

        if (!radioInfo.IsConnected) return;
        await _client.EnqueueAsync(message);
    }
}