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
    public int index;
    private Eurotherm Controller;
    private StepperController? stepper;
    private Capture? camera;
    private CancellationTokenSource? _cts;
    private bool isEnabled = false;

    public Furnace(FurnaceInit init, Channel<FurnaceSet> setChannel, Channel<FurnaceState> stateChannel, int _index)
    {
        index = _index;
        Controller = new Eurotherm(init.eurothermIp, init.eurothermPort);
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

    public async Task Run(CancellationToken token)
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