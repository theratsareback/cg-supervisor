namespace furnace;
using System;
using System.Net.Sockets;
using System.Threading;

/// <summary>
/// Class <c>FurnaceInit</c> is a datatype containing the values to initialize one furnace. This is stored in a .JSON on the backend computers
/// </summary>
public class FurnaceInit
{
    public string furnaceLabel;
    public int index;
    public int eurothermPort;
    public string eurothermIp;

    public int cameraPort;
    public string cameraIp;

    public string pullerIp;
    public byte pullerSlaveId;
    public byte rotaterSlaveId;

    public FurnaceInit(string _furnaceLabel, int _index, int _eurothermPort, string _eurothermIp, int _cameraPort, string _cameraIp, string _pullerIp, byte _pullerSlaveId, byte _rotaterSlaveId)
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

/// <summary>
/// Class <c>FurnaceState</c> contains information about a single furnace's current state
/// </summary>
public class FurnaceState
{
    public bool _active;
    public string? furnaceLabel;
    public double processValue;
    public double setpoint;
    public double heaterCurrent;
    public FurnaceStatus status;
    public AlarmStatus underrangeAlarm;
    public AlarmStatus overrangeAlarm;
    public AlarmStatus sensorBreak;
    public AlarmStatus rspFailure;
    public ProfileStatus state;
    public long time_s;

    public FurnaceState(string _furnaceLabel, string _profileName, double _processValue, double _setpoint, FurnaceStatus _status, AlarmStatus _underrange, AlarmStatus _overrange, AlarmStatus _sensor, AlarmStatus _rsp, ProfileStatus _state, long _time_s)
    {
        _active = true;
        furnaceLabel = _furnaceLabel;
        processValue = _processValue;
        setpoint = _setpoint;
        status = _status;
        underrangeAlarm = _underrange;
        overrangeAlarm = _overrange;
        sensorBreak = _sensor;
        rspFailure = _rsp;
        state = _state;
        time_s = _time_s;
    }
    public FurnaceState()
    {
        _active = false;
    }
}

public enum ProcessState { Stop, Pause, Continue }
public enum ProfileStatus { Running, Paused, Stopped }
public enum FurnaceStatus { Enabled, Disabled, Alarm }
public enum AlarmStatus {Off, OnAck, OffNonAck, OnNonAck}

/// <summary>
/// Struct <c>FurnaceSet</c> contains the state of the frontend interface
/// </summary>
public struct FurnaceSet
{
    public double setpoint;
    public double trim;
    public bool manualSetpoint;
    public bool enable;
    public ProcessState state;
    public Profile? setProfile;
    // TODO motor speeds and alarm acknowledgements
}