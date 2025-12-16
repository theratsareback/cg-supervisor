namespace furnace;

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class InvalidFurnaceException : Exception
{
    public InvalidFurnaceException() : base("Entered furnace is not valid.") { }
    public InvalidFurnaceException(string message) : base(message) { }
}

/// <summary>
/// Class <c>Furnace</c> represents one Eurotherm and Camera pair.
/// </summary>
public class Furnace
{
    private Eurotherm Controller;
    private StepperController stepper;
    private Capture camera;
    public FurnaceInit init;
    private CancellationTokenSource? _cts;
    private bool isEnabled;

    static BoundedChannelOptions channelOptions = new(capacity: 1)
    {
        FullMode = BoundedChannelFullMode.DropOldest, // drop the current value to keep only the newest
        SingleWriter = true,
        SingleReader = true
    };

    Channel<FurnaceState> loopOut = Channel.CreateBounded<FurnaceState>(channelOptions);
    Channel<FurnaceState> loopIn = Channel.CreateBounded<FurnaceState>(channelOptions);

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

        List<FurnaceInit>? furnaces = JsonConvert.DeserializeObject<List<FurnaceInit>>(File.ReadAllText(@"furnaces.json"));

        if (furnaces == null)
        {
            furnaces = []; // make empty list if file is null
            throw new InvalidFurnaceException("No existing furnaces");
        }

        for (int i = 0; i < furnaces.Count; i++)
        {
            if (furnaces[i].index == index)
            {
                init = furnaces[i];
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
    /// <param name="pullerIp">IPV4 address for the Modbus TCP/IP to RTU adapter</param>
    /// <param name="pullerSlaveId">Modbus slave ID for the puller stepper driver</param>
    /// <param name="rotaterSlaveId">Modbus slave ID for the rotater stepper driver</param>
    public Furnace(byte index, string furnaceLabel, int eurothermPort, string eurothermIp, int camPort, string camIp, string pullerIp, byte pullerSlaveId, byte rotaterSlaveId)
    {
        if (!File.Exists(@"furnaces.json"))
        {
            File.WriteAllText(@"furnaces.json", "[]"); // if file doesn't exist, make one with an empty list
        }

        List<FurnaceInit>? furnaces = JsonConvert.DeserializeObject<List<FurnaceInit>>(File.ReadAllText(@"furnaces.json"));

        furnaces ??= []; // make empty list if file is null

        if (furnaces.Count > 0) //if list isn't empty
        {
            for (int i = 0; i < furnaces.Count; i++)  // look for existing furnace
            {
                if (furnaces[i].index == index)
                {
                    init = furnaces[i];
                    furnaces[i].eurothermIp = eurothermIp; // update existing FurnaceData instance to new entry
                    furnaces[i].eurothermPort = eurothermPort;
                    furnaces[i].furnaceLabel = furnaceLabel;
                    furnaces[i].cameraPort = camPort;
                    furnaces[i].cameraIp = camIp;
                    furnaces[i].pullerIp = pullerIp;
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
        FurnaceInit newFurnace; // if existing FurnaceData not found, make new one and append to list
        newFurnace = new FurnaceInit(furnaceLabel, index, eurothermPort, eurothermIp, camPort, camIp, pullerIp, pullerSlaveId, rotaterSlaveId);
        init = newFurnace;
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
        if (!isEnabled)
        {
            Controller.Heater.Enable();
            isEnabled = true;
        }
    }

    /// <summary>
    /// Disable heating element output
    /// </summary>
    public void Disable()
    {   
        if (isEnabled)
        {
            Controller.Heater.Disable();
            isEnabled = false;
        }
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
    /// This could be used to make a reference graphic where 0 is a green circle, 1 is grey, and 2/3 are both red.
    /// </summary>
    /// <returns></returns>
    public FurnaceStatus GetStatus()
    {
        FurnaceStatus status = Controller.Heater.CheckHeaterStatus();
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
    public AlarmStatus GetAlarm(byte alarmIndex)
    {
        AlarmStatus status = Controller.alarms[alarmIndex].GetStatus();
        return status;
    }

    // public Circle GetBestFit()
    // {
    //     return camera.GetBestFit();
    // }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => Run(_cts.Token));
    }

    private async Task Run(CancellationToken token)
    {
        // ProfileHandler handler = new ProfileHandler();

        // while (!token.IsCancellationRequested)
        // {
        //     FurnaceState setState = await loopIn.Reader.ReadAsync(token);
        //     handler.activeProfile = setState.activeProfile;

        //     if (setState.isRunning)
        //     {
        //         handler.Start();
        //         Enable();
        //     }
        //     else if (setState.stopped)
        //     {
        //         handler.Stop();
        //         Disable();
        //     }
        //     else
        //     {
        //         handler.Pause();
        //         Enable();
        //     }
        //     if (!setState.stopped)
        //     {
        //         setState.setpoint = handler.activeProfile.GetSetpoint();
        //         SetSetpoint(setState.trim + setState.setpoint);
        //     }
        //     setState.processValue = GetProcessValue();
        // }
    }
}