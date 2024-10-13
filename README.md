# OmniRigMQTT

- A OmniRig to MQTT &amp; N1MM UDP generator

The purpose of this application is to provide band / frequency information 
in such a way that it doesn't interfere with any existing configuration; assuming you use OmniRig.

This application serves as an API & bridge between OmniRig and MQTT.


For details on "N1MM"-style UDP datagram generation please skip the MQTT section.

## MQTT Topics

OmniRigMQTT uses several MQTT topics for communication. Here's an overview of the topics and their purposes:

### Command Topics

- **Topic**: `omnirig/{rig_id}/commands`
- **Purpose**: Send commands to control the radio
- **Direction**: Publish to this topic
- **Example**: `omnirig/1/commands`

### Response Topics

- **Topic**: `omnirig/{rig_id}/responses`
- **Purpose**: Receive responses to commands
- **Direction**: Subscribe to this topic
- **Example**: `omnirig/1/responses`

### Status Topics

- **Topic**: `omnirig/{rig_id}/status`
- **Purpose**: Receive periodic status updates about the radio
- **Direction**: Subscribe to this topic
- **Example**: `omnirig/1/status`

### Event Topics

- **Topic**: `omnirig/{rig_id}/events`
- **Purpose**: Receive event notifications (e.g., connection status changes, errors)
- **Direction**: Subscribe to this topic
- **Example**: `omnirig/1/events`

### Radio Info Topics

OmniRigMQTT publishes radio information to two special topics:

1. **Frequent Updates**
    - **Topic**: `omnirig/frequent/radio_info`
    - **Purpose**: Receive frequent updates of radio information
    - **Direction**: Subscribe to this topic
    - **Update Interval**: Typically every 500ms (configurable)
    - **Note**: If the contents of `radio_info` are the same as last time, it will not return anything.

2. **Sporadic Updates**
    - **Topic**: `omnirig/sporadic/radio_info`
    - **Purpose**: Receive less frequent, comprehensive updates of radio information
    - **Direction**: Subscribe to this topic
    - **Update Interval**: Typically every 2 minutes (configurable)

### Notes

- Replace `{rig_id}` with the identifier of the specific rig you're controlling (e.g., "1" for the first rig).
- All topics use QoS 1 (At least once) to ensure message delivery.
- Status and radio info topics use the retain flag, so new subscribers will immediately receive the last known state.

To interact with OmniRigMQTT:
1. Subscribe to the response, status, event, and radio info topics you're interested in.
2. Publish commands to the command topic.
3. Listen for responses and updates on the subscribed topics.

#### Command structure:

```json
 {
     "command": "command_name",
     "parameters": {
         "param1": value1,
         "param2": value2
     }
 }
```

```json
Payload: Freq: 28074000
TxFreq: 28075000
Mode: USB
IsTransmitting: false
IsSplit: true
IsConnected: true
ActiveRadioNr: 1
RadioName: TS-590
```

## Supported MQTT Commands

OmniRigMQTT supports the following commands via MQTT:

1. **Set Frequency**
    - Sets the frequency for VFO A or B
    - Example:
      ```json
      {
        "command": "set_frequency",
        "parameters": {
          "frequency": 14074000,
          "vfo": "A"
        }
      }
      ```
    - Note: If "vfo" is omitted, it defaults to VFO A

2. **Set Mode**
    - Sets the radio mode
    - Example:
      ```json
      {
        "command": "set_mode",
        "parameters": {
          "mode": "USB"
        }
      }
      ```

3. **Set PTT** (This is not implemented yet)
    - Sets the Push-To-Talk (PTT) state
    - Example:
      ```json
      {
        "command": "set_ptt",
        "parameters": {
          "ptt": true
        }
      }
      ```

4. **Get Status** (returns the whole `RadioInfo` structure)
    - Retrieves the current radio status
    - Example:
      ```json
      {
        "command": "get_status",
        "parameters": {}
      }
      ```

These commands should be published to the topic `omnirig/{rig_id}/commands`, where `{rig_id}` is the identifier for t
specific rig you're controlling.

Responses will be published to `omnirig/{rig_id}/responses` or to the response topic specified in the MQTT message
properties.

---

## UDP N1MM Messages

 OmniRigMQTT interacts with N1MM Logger+ via UDP messages. Here's an overview of the UDP communication:

 ### Receiving UDP Messages

 - **Port**: 12060 (default, configurable)
 - **Purpose**: Receive commands from N1MM Logger+ & similar. VERY LIMITED implementation
 - **Direction**: Incoming to OmniRigMQTT

 OmniRigMQTT listens for UDP messages on this port. These messages contain commands to control the radio.

 ### Sending UDP Messages

 - **Address**: localhost (default, configurable)
 - **Port**: 12060 (default, configurable)
 - **Purpose**: Send radio information to N1MM Logger+ (or other loggers that support this style of datagrams)
 - **Direction**: Outgoing from OmniRigMQTT

 OmniRigMQTT sends UDP messages to this address and port. These messages contain information about the radio state as
 determined by OmniRig.

 ### Message Format

 The UDP messages sent by OmniRigMQTT use an [XML format similar to N1MM+](https://n1mmwp.hamdocs.com/appendices/external-udp-broadcasts/#xml-schema-and-message-field-lists). 
 

 ```xml
 <?xml version="1.0" encoding="utf-8"?>
 <RadioInfo>
   <app>N1MM-Gen</app>
   <StationName>Test</StationName>
   <RadioNr>1</RadioNr>
   <Freq>14074000</Freq>
   <TXFreq>14074000</TXFreq>
   <Mode>USB</Mode>
   <OpCall>LB1TI</OpCall>
   <IsRunning>False</IsRunning>
   <FocusEntry>204626</FocusEntry>
   <EntryWindowHwnd>275678</EntryWindowHwnd>
   <Antenna>8</Antenna>
   <Rotors></Rotors>
   <FocusRadioNr>1</FocusRadioNr>
   <IsStereo>False</IsStereo>
   <IsSplit>False</IsSplit>
   <ActiveRadioNr>1</ActiveRadioNr>
   <IsTransmitting>False</IsTransmitting>
   <FunctionKeyCaption></FunctionKeyCaption>
   <RadioName></RadioName>
   <AuxAntSelected>-1</AuxAntSelected>
   <AuxAntSelectedName></AuxAntSelectedName>
   <IsConnected>True</IsConnected>
 </RadioInfo>
```

Note: Some fields in this XML are static (e.g., StationName, OpCall) and don't reflect actual radio state. They are
included to adhere to the packet structure.

---

### Configuration

The UDP sender and receiver ports can be configured using command-line arguments or by editing the config.json file.
Here are the relevant configuration options:

- **UdpSender.Address**: The address to send UDP messages to (default: "localhost")
- **UdpSender.Port**: The port to send UDP messages to (default: 12060)
- **UdpReceiverPort**: The port to receive UDP messages on (default: 12060)

To change these settings via command-line:

* `OmniRigMQTT.exe --set-sender-address 127.0.0.1 12345`
* `OmniRigMQTT.exe --set-receiver-port 12345`