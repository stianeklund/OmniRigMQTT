using System.Runtime.InteropServices;
using OmniRig;

namespace OmniRigMQTT;

public class OmniRigInterface
{
    private readonly OmniRigX _omniRigX;
    private readonly RigX _rig1;
    private readonly RigX _rig2;
    private RadioInfo _radioInfo;

    public OmniRigInterface()
    {
        try
        {
            _omniRigX = new OmniRigX();
            _omniRigX.CustomReply += OmniRigXOnCustomReply;
            _omniRigX.ParamsChange += OmniRigXOnParamsChange;
            _rig1 = _omniRigX.Rig1;
            _rig2 = _omniRigX.Rig2;
        }
        catch (COMException ex)
        {
            Console.WriteLine($"Error initializing OmniRig: {ex.Message}");
            throw;
        }
    }

    public int ActiveRadioNr { get; set; }

    public bool SetSimplex()
    {
        _rig1.SetSimplexMode(_rig1.Freq);
        return _rig1.Split == RigParamX.PM_SPLITOFF;
    }

    public bool SetSplitMode()
    {
        _rig1.SetSplitMode(_rig1.FreqA, _rig1.FreqB);
        return _rig1.Split == RigParamX.PM_SPLITON;
    }


    private void OmniRigXOnParamsChange(int rignumber, int @params)
    {
        // throw new NotImplementedException();
    }
    // Add other properties as needed
    /*<app>N1MM</app>
    <StationName>CW-80m</StationName>
    <RadioNr>1</RadioNr>
    <Freq>352211</Freq>
    <TXFreq>352211</TXFreq>
    <Mode>CW</Mode>
    <OpCall>W1ABC</OpCall>
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
    <IsConnected></IsConnected>*/

    public async Task<RadioInfo> GetRadioInfoAsyncRig1()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!IsOmniRigAvailable()) throw new InvalidOperationException("OmniRig is not available.");

                return new RadioInfo(_rig1.GetRxFrequency(), _rig1.GetTxFrequency(), ParseMode(_rig1.Mode),
                    _rig1.Tx == RigParamX.PM_TX, _rig1.Split == RigParamX.PM_SPLITON,
                    _rig1.Status == RigStatusX.ST_ONLINE, _rig1.Status == RigStatusX.ST_ONLINE ? 1 : 2,
                    _rig1.RigType);
                /*{
                    Freq = _rig1.GetRxFrequency(),
                    TxFreq = _rig1.GetTxFrequency(),
                    Mode = ParseMode(_rig1.Mode),
                    IsTransmitting = _rig1.Tx == RigParamX.PM_TX,
                    ActiveRadioNr = _rig1.Status == RigStatusX.ST_ONLINE ? 1 : 2,
                    RadioName = _rig1.RigType,
                    IsConnected = _rig1.Status == RigStatusX.ST_ONLINE,
                    IsSplit = _rig1.Split == RigParamX.PM_SPLITON
                };*/
            }
            catch (COMException ex)
            {
                Console.WriteLine($"Error getting radio info: {ex.Message}");
                throw;
            }
        });
    }

    public async Task<RadioInfo> GetRadioInfoAsyncRig2()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!IsOmniRigAvailable()) throw new InvalidOperationException("OmniRig is not available.");

                return new RadioInfo(_rig2.GetRxFrequency(), _rig2.GetTxFrequency(),
                    ParseMode(_rig2.Mode), _rig2.Tx == RigParamX.PM_TX,
                    activeRadioNr: _rig2.Status == RigStatusX.ST_ONLINE ? 2 : 1, radioName: _rig2.RigType,
                    isConnected: _rig2.Status == RigStatusX.ST_ONLINE, isSplit: _rig2.Split == RigParamX.PM_SPLITON);
            }
            catch (COMException ex)
            {
                Console.WriteLine($"Error getting radio info: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    ///     Parses the omnirig mode into a nicer string
    /// </summary>
    private static string ParseMode(RigParamX mode)
    {
        return mode switch
        {
            RigParamX.PM_CW_U => "CW",
            RigParamX.PM_CW_L => "CW-R",
            RigParamX.PM_SSB_U => "USB",
            RigParamX.PM_SSB_L => "LSB",
            RigParamX.PM_DIG_U => "USB-D",
            RigParamX.PM_DIG_L => "LSB-D",
            RigParamX.PM_AM => "AM",
            RigParamX.PM_FM => "FM",
            _ => "None"
        };
    }

    public static RigParamX ParseMode(string mode)
    {
        return mode switch
        {
            "CW" => RigParamX.PM_CW_U,
            "CW-R" => RigParamX.PM_CW_L,
            "USB" => RigParamX.PM_SSB_U,
            "LSB" => RigParamX.PM_SSB_L,
            "USB-D" => RigParamX.PM_DIG_U,
            "LSB-D" => RigParamX.PM_DIG_L,
            "AM" => RigParamX.PM_AM,
            "FM" => RigParamX.PM_FM,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private bool IsOmniRigAvailable()
    {
        try
        {
            _ = _rig1.FreqA;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task SetVfoAFrequencyAsync(long frequencyHz)
    {
        await Task.Run(() =>
        {
            try
            {
                if (!IsOmniRigAvailable()) throw new InvalidOperationException("OmniRig is not available.");

                _rig1.FreqA = (int)frequencyHz;
            }
            catch (COMException ex)
            {
                Console.WriteLine($"Error setting radio frequency: {ex.Message}");
                throw;
            }
        });
    }

    public async Task SetVfoBFrequencyAsync(long frequencyHz)
    {
        await Task.Run(() =>
        {
            try
            {
                if (!IsOmniRigAvailable()) throw new InvalidOperationException("OmniRig is not available.");

                _rig1.FreqB = (int)frequencyHz;
            }
            catch (COMException ex)
            {
                Console.WriteLine($"Error setting radio frequency: {ex.Message}");
                throw;
            }
        });
    }

    public void SendCustomCommand(string command)
    {
        try
        {
            if (command.EndsWith(';')) command = command.TrimEnd(';');

            _rig1.SendCustomCommand(command, 0, ';');
        }
        catch (COMException ex)
        {
            Console.WriteLine($"Error sending custom command: {ex.Message}");
        }
    }

    private void OmniRigXOnCustomReply(int rigNumber, object command, object reply)
    {
        Console.WriteLine($"Custom Reply - Rig: {rigNumber}, Command: {command}, Reply: {reply}");

        if (command.ToString() == "IF;") ParseIfCommandReply(reply.ToString());
    }

    private void ParseIfCommandReply(string? reply)
    {
        // IF command reply format: IF[f]*****[m][r][t][s][x][b][v]*****;
        // Where:
        // [f]: Operating frequency (11 digits)
        // [m]: Mode (2 digits)
        // [r]: RIT status (1 digit)
        // [t]: XIT status (1 digit)
        // [s]: Memory channel number (2 digits)
        // [x]: TX/RX status (1 digit)
        // [b]: Operating band (2 digits)
        // [v]: VFO/Memory status (1 digit)

        if (reply == null || reply.Length < 38)
        {
            Console.WriteLine("Invalid IF command reply length");
            return;
        }

        var frequency = long.Parse(reply.Substring(2, 11));
        var mode = ParseMode(int.Parse(reply.Substring(13, 2)));
        var ritOn = reply[15] == '1';
        var xitOn = reply[16] == '1';
        var memoryChannel = int.Parse(reply.Substring(17, 2));
        var isTransmitting = reply[19] == '1';
        var operatingBand = int.Parse(reply.Substring(20, 2));
        var isVfoMode = reply[22] == '0';

        Console.WriteLine($"Frequency: {frequency} Hz");
        Console.WriteLine($"Mode: {mode}");
        Console.WriteLine($"RIT: {(ritOn ? "ON" : "OFF")}");
        Console.WriteLine($"XIT: {(xitOn ? "ON" : "OFF")}");
        Console.WriteLine($"Memory Channel: {memoryChannel}");
        Console.WriteLine($"TX/RX: {(isTransmitting ? "TX" : "RX")}");
        Console.WriteLine($"Operating Band: {operatingBand}");
        Console.WriteLine($"VFO/Memory: {(isVfoMode ? "VFO" : "Memory")}");

        // Update RadioInfo with the parsed information
        UpdateRadioInfo(frequency, mode, isTransmitting, isVfoMode);
    }

    private void UpdateRadioInfo(long frequency, string mode, bool isTransmitting, bool isVfoMode)
    {
        // Assuming we're updating RadioInfo for Rig1
        _radioInfo = new RadioInfo(frequency, frequency, // Assuming simplex operation
            mode, isTransmitting, activeRadioNr: 1, // Assuming Rig1
            radioName: _rig1.RigType, isConnected: _rig1.Status == RigStatusX.ST_ONLINE,
            isSplit: _rig1.Split == RigParamX.PM_SPLITON);

        // You might want to store this updated RadioInfo somewhere or emit an event with the new data
        Console.WriteLine("RadioInfo updated with IF command data");
    }

    private string ParseMode(int modeCode)
    {
        return modeCode switch
        {
            1 => "LSB",
            2 => "USB",
            3 => "CW",
            4 => "FM",
            5 => "AM",
            6 => "FSK",
            7 => "CW-R",
            9 => "FSK-R",
            _ => "Unknown"
        };
    }

    public Task SetModeAsync(RigParamX rigParamX)
    {
        _rig1.Mode = rigParamX;
        return Task.CompletedTask;
    }
}
