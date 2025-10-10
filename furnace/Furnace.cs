namespace furnace;

using System;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;

public class InvalidFurnaceException : Exception
{
    public InvalidFurnaceException() : base("Entered furnace is not valid.") { }
    public InvalidFurnaceException(string message) : base(message) { }
}

/// <summary>
/// Class <c>FurnaceData</c> is a datatype for storing furnaces as JSON objects.
/// </summary>
class FurnaceData
{
    public string furnaceLabel;
    public byte index;
    public int eurothermPort;
    public string eurothermIp;

    public int cameraPort;
    public string cameraIp;

    public string modbusAdapterIp;
    public byte pullerSlaveId;
    public byte rotaterSlaveId;

    public FurnaceData(string _furnaceLabel, byte _index, int _eurothermPort, string _eurothermIp, int _cameraPort, string _cameraIp, string _modbusAdapterIp, byte _pullerSlaveId, byte _rotaterSlaveId)
    {
        furnaceLabel = _furnaceLabel;
        index = _index;
        eurothermPort = _eurothermPort;
        eurothermIp = _eurothermIp;
        cameraPort = _cameraPort;
        cameraIp = _cameraIp;
        modbusAdapterIp = _modbusAdapterIp;
        pullerSlaveId = _pullerSlaveId;
        rotaterSlaveId = _rotaterSlaveId;
    }
}

/// <summary>
/// Class <c>Furnace</c> represents one Eurotherm and Camera pair.
/// </summary>
public class Furnace
{
    private Eurotherm Controller;
    private StepperController stepper;
    private Capture camera;

    /// <summary>
    /// Constructor <c>Furnace</c> with param <c>index</c> looks for an existing furnace in <c>furnaces.json</c>. <br/>
    /// Constructor <c>Furnace</c> with params <c>index, furnaceLabel, eurothermPort, eurothermIp, cameraPort, cameraIp</c>
    /// updates an existing furnace with the same index, or if no furnace exists with that index, creates a new furnace. <br/>
    /// Updated or new furnace is automatically added to the furnaces.json file.
    /// </summary>
    /// <param name="index">Unique identifier for the furnace</param>
    /// <exception cref="InvalidFurnaceException"></exception>
    public Furnace(byte index)
    {
        if (!File.Exists(@"furnaces.json"))
        {
            File.WriteAllText(@"furnaces.json", "[]"); // if file doesn't exist, make one with an empty list
            throw new InvalidFurnaceException("No existing furnaces");
        }

        List<FurnaceData>? furnaces = JsonConvert.DeserializeObject<List<FurnaceData>>(File.ReadAllText(@"furnaces.json"));

        if (furnaces == null)
        {
            furnaces = []; // make empty list if file is null
            throw new InvalidFurnaceException("No existing furnaces");
        }

        for (int i = 0; i < furnaces.Count; i++)
        {
            if (furnaces[i].index == index)
            {
                Controller = new Eurotherm(furnaces[i].eurothermIp, furnaces[i].eurothermPort);
                //camera = new Capture(furnaces[i].cameraIp, furnaces[i].cameraPort);
                //camera.Start();
                // TODO STEPPERS
                return;
            }
        }
        throw new InvalidFurnaceException($"Invalid furnace index {index}");
    }

    /// <summary>
    /// Constructor <c>Furnace</c> with param <c>index</c> looks for an existing furnace in <c>furnaces.json</c>. <br/>
    /// Constructor <c>Furnace</c> with params <c>index, furnaceLabel, eurothermPort, eurothermIp, cameraPort, cameraIp</c>
    /// updates an existing furnace with the same index, or if no furnace exists with that index, creates a new furnace. <br/>
    /// Updated or new furnace is automatically added to the furnaces.json file.
    /// </summary>
    /// <param name="index">Unique identifier for the furnace</param>
    /// <param name="furnaceLabel">User indentifier for the furnace ("PbMO4 furnace 3")</param>
    /// <param name="eurothermPort">Network port used by the Eurotherm, default 502</param>
    /// <param name="eurothermIp">IPV4 address for the Eurotherm</param>
    /// <param name="camPort">Network port for the Camera</param>
    /// <param name="camIp">IPV4 address for the Camera</param>
    /// <param name="modbusAdapterIp">IPV4 address for the Modbus TCP/IP to RTU adapter</param>
    /// <param name="pullerSlaveId">Modbus slave ID for the puller stepper driver</param>
    /// <param name="rotaterSlaveId">Modbus slave ID for the rotater stepper driver</param>
    public Furnace(byte index, string furnaceLabel, int eurothermPort, string eurothermIp, int camPort, string camIp, string modbusAdapterIp, byte pullerSlaveId, byte rotaterSlaveId)
    {
        if (!File.Exists(@"furnaces.json"))
        {
            File.WriteAllText(@"furnaces.json", "[]"); // if file doesn't exist, make one with an empty list
        }

        List<FurnaceData>? furnaces = JsonConvert.DeserializeObject<List<FurnaceData>>(File.ReadAllText(@"furnaces.json"));

        furnaces ??= []; // make empty list if file is null

        if (furnaces.Count > 0) //if list isn't empty
        {
            for (int i = 0; i < furnaces.Count; i++)  // look for existing furnace
            {
                if (furnaces[i].index == index)
                {
                    furnaces[i].eurothermIp = eurothermIp; // update existing furnacedata instance to new entry
                    furnaces[i].eurothermPort = eurothermPort;
                    furnaces[i].furnaceLabel = furnaceLabel;
                    furnaces[i].cameraPort = camPort;
                    furnaces[i].cameraIp = camIp;
                    furnaces[i].modbusAdapterIp = modbusAdapterIp;
                    furnaces[i].pullerSlaveId = pullerSlaveId;
                    furnaces[i].rotaterSlaveId = rotaterSlaveId;
                    Controller = new Eurotherm(eurothermIp, eurothermPort);
                    Controller.Connect();
                    File.WriteAllText(@"furnaces.json", JsonConvert.SerializeObject(furnaces, Formatting.Indented)); // update json array and write to file
                    //camera = new Capture(camIp, camPort);
                    //camera.Start();
                    //TODO MOTORS
                    return;
                }
            }
        }
        FurnaceData newFurnace; // if existing FurnaceData not found, make new one and append to list
        newFurnace = new FurnaceData(furnaceLabel, index, eurothermPort, eurothermIp, camPort, camIp, modbusAdapterIp, pullerSlaveId, rotaterSlaveId);
        furnaces.Add(newFurnace);
        File.WriteAllText(@"furnaces.json", JsonConvert.SerializeObject(furnaces, Formatting.Indented)); // update json array and write to file

        // then finish making Furnace instance
        Controller = new Eurotherm(eurothermIp, eurothermPort);
        Controller.Connect();
        //camera = new Capture(camIp, camPort);
        //camera.Start();
        //TODO MOTORS
        return;
    }

    public void SetSetpoint(double setpoint)
    {
        Controller.Heater.SP(setpoint);
    }

    /// <summary>
    /// Enable heating element output
    /// </summary>
    public void Enable()
    {
        Controller.Heater.Enable();
    }

    /// <summary>
    /// Disable heating element output
    /// </summary>
    public void Disable()
    {
        Controller.Heater.Disable();
    }

    public float GetProcessValue()
    {
        float pv = Controller.Heater.PV();
        return pv;
    }

    /// <summary>
    /// Checks the status of the furnace's Eurotherm.
    /// Returns a value of 0 for active, 1 if no alarms are on but the heater is disabled, 
    /// 2 if there is an active alarm and the furnace is enabled, and 3 if the furnace is disabled with an alarm.
    /// This could be used to make a reference graphic where 0 is a green circle, 1 is yellow, and 2/3 are both red.
    /// </summary>
    /// <returns></returns>
    public byte GetStatus()
    {
        byte status = Controller.Heater.CheckHeaterStatus();
        return status;
    }

    /// <summary>
    /// Get the value of a specified alarm.
    /// </summary>
    /// <param name="alarmIndex">Identifier for alarms. 0 is overrange alarm,
    ///  1 is underrange, 2 is sensor break, 3 is remote setpoint failure.</param>
    /// <returns>
    /// 0 if alarm is off, 1 for active but acknowledged, 
    /// 2 for inactive not acknowledged, 3 for active not acknowledged
    /// </returns>
    public byte GetAlarm(byte alarmIndex)
    {
        byte status = Controller.alarms[alarmIndex].GetStatus();
        return status;
    }

    // public Circle GetBestFit()
    // {
    //     return camera.GetBestFit();
    // }
}