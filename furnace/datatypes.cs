using System.Reflection.Metadata;
using System.Text.Json.Serialization;
using OpenCvSharp;

namespace furnace;

/// <summary>
/// Record <c>FurnaceInit</c> is a datatype containing the values to initialize one furnace. This is stored in a .JSON on the backend computers.
/// To create a new furnace, an instance of this object must be created on the frontend and sent to the backend.
/// </summary>
public readonly record struct FurnaceInit
{
    public required string furnaceLabel {get; init;}
    public int eurothermPort {get; init;}
    public required string eurothermIp {get; init;}
    public int cameraPort {get; init;}
    public required string cameraIp {get; init;}
    public required string pullerIp {get; init;}
    public byte pullerPort {get; init;}
    public double diameter_kp {get; init;}
    public double diameter_ti {get; init;}
    public double diameter_td {get; init;}
}

/// <summary>
/// Class <c>FurnaceState</c> contains information about a single furnace's current state, to be sent to the frontend
/// </summary>
public readonly record struct FurnaceState
{
    public required string furnaceLabel {get; init;}
    public double processValue {get; init;}
    public double setpoint {get; init;}
    public double heaterCurrent {get; init;}
    public double diameterTrim {get; init; }
    public FurnaceStatus furnaceStatus {get; init;}
    public AlarmStatus underrangeAlarm {get; init;}
    public AlarmStatus overrangeAlarm {get; init;}
    public AlarmStatus sensorBreak {get; init;}
    public AlarmStatus rspFailure {get; init;}
    public ProfileStatus profileStatus {get; init;}
    public long time_s {get; init;}
}

public enum ProfileStatus { Running, Paused, Stopped }

/// <summary>
/// FurnaceStatus communicates whether the furnace is enabled, disabled, or in an alarm state.
/// </summary>
public enum FurnaceStatus { Enabled, Disabled, Alarm }
public enum AlarmStatus { Off, OnAck, OffNonAck, OnNonAck }

public enum EventType 
{   
    NewFurnace, 
    RemoveFurnace, 
    ModifyFurnace, 
    NewProfile, 
    RemoveProfile, 
    ModifyProfile, 
    RequestProfiles, 
    RequestFurnaces, 
    SetFurnaceProfile, 
    SetProfileStatus, 
    AckFurnaceAlarm, 
    SetSteppers, 
    Enable, 
    SetSetpoint,
    StartDiameterControl,
    SetManTrim,
    SetGains,
    SeekTime
}

/// <summary>
/// Struct <c>FurnaceSet</c> contains the state of the frontend interface to be streamed to the backend.
/// Old information is ignored. Used only to convey continuous data.
/// </summary>
public class FurnaceSet
{
    public double setpoint; // to be deprecated
    public double trim; //deprecated
    public bool manualSetpoint; // to be deprecated
    public bool enable; // deprecated
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
    public string? Label { get; init; }
    public List<Segment> Segments { get; } = new();
}

/// <summary>
/// Represents one segment of a profile
/// </summary>
public class Segment
{
    /// <summary>
    /// Type 1 is a ramp segment, 2 is dwell, 3 is pause
    /// </summary>
    public byte Type { get; set; }
    public byte Index { get; set; }
    public uint Duration { get; set; }
    public double Endpoint { get; set; }

}

