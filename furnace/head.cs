namespace furnace;
using System;
using System.Net.Sockets;
using System.Threading;

/// <summary>
/// Class <c>FurnaceInit</c> is a datatype representing the state of one furnace.
/// </summary>
public class FurnaceInit
{
    public string furnaceLabel;
    public byte index;
    public int eurothermPort;
    public string eurothermIp;

    public int cameraPort;
    public string cameraIp;

    public string pullerIp;
    public byte pullerSlaveId;
    public byte rotaterSlaveId;

    public FurnaceInit(string _furnaceLabel, byte _index, int _eurothermPort, string _eurothermIp, int _cameraPort, string _cameraIp, string _pullerIp, byte _pullerSlaveId, byte _rotaterSlaveId)
    {
        furnaceLabel = _furnaceLabel;
        index = _index;
        eurothermPort = _eurothermPort;
        eurothermIp = _eurothermIp;
        cameraPort = _cameraPort;
        cameraIp = _cameraIp;
        pullerIp = _pullerIp;
        pullerSlaveId = _pullerSlaveId;
        rotaterSlaveId = _rotaterSlaveId;
    }
}

public struct FurnaceState
{
    public string profileName;
    public double processValue;
    public double setpoint;
    public FurnaceStatus status;
    public AlarmStatus underrangeAlarm;
    public AlarmStatus overrangeAlarm;
    public AlarmStatus sensorBreak;
    public AlarmStatus rspFailure;
    public ProcessState state;
    public long time_s;
}

public enum ProcessState { None, Pause, Resume }
public enum FurnaceStatus { Enabled, Disabled, Alarm }
public enum AlarmStatus {Off, OnAck, OffNonAck, OnNonAck}

public struct FurnaceSet
{
    public double setpoint;
    public double trim;
    public ProcessState state;
    public Profile setProfile;
    // TODO motor speeds and alarm acknowledgements
}