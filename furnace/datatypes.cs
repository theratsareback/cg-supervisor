using OpenCvSharp;

namespace furnace;

/// <summary>
/// Class <c>FurnaceInit</c> is a datatype containing the values to initialize one furnace. This is stored in a .JSON on the backend computers.
/// To create a new furnace, an instance of this object must be created on the frontend and sent to the backend.
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
/// Class <c>FurnaceState</c> contains information about a single furnace's current state, to be sent to the frontend
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

public enum ProcessState { Stop, Pause, Continue } // need to rewrite as an event instead of stream
public enum ProfileStatus { Running, Paused, Stopped }
public enum FurnaceStatus { Enabled, Disabled, Alarm }
public enum AlarmStatus { Off, OnAck, OffNonAck, OnNonAck }
public enum EventType { NewFurnace, RemoveFurnace, ModifyFurnace, NewProfile, RemoveProfile, ModifyProfile, RequestProfiles, RequestFurnaces }

/// <summary>
/// Struct <c>FurnaceSet</c> contains the state of the frontend interface to be streamed to the backend.
/// Old information is ignored. Used only to convey continuous data.
/// </summary>
public struct FurnaceSet
{
    public double setpoint;
    public double trim;
    public bool manualSetpoint;
    public bool enable;
    public ProcessState state;
    public ProfileDef? setProfile;
    // TODO motor speeds and alarm acknowledgements
}

/// <summary>
/// Represents a discrete event and object to be used for the event.
/// These are processed in a queue. No event is ignored.
/// </summary>
public class Event<T>
{
    public EventType type;
    public T? EventObject;

    public Event(EventType _type, T? obj = default)
    {
        type = _type;
        EventObject = obj;
    }
}

/// <summary>
/// circle.
/// </summary>
public class Circle
{
    public Point2f Center { get; set; }
    public float Radius { get; set; }
    public double AccumulatorScore { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// JSON convertable profile definition to be handed to profile handler
/// </summary>
public sealed class ProfileDef
{
    public string Label { get; init; }
    public int profileIndex;
    public List<Segment> Segments { get; } = new();
}

/// <summary>
/// Represents one segment of a profile
/// </summary>
public class Segment
{
    public byte Type { get; set; }
    // Type 1 is ramp, 2 is dwell, 3 is pause
    public byte Index { get; set; }
    public uint Duration { get; set; }
    public double Endpoint { get; set; }

}