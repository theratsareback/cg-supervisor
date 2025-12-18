namespace furnace;

using System;
using System.Data;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Newtonsoft.Json;

/// <summary>
/// Class <c>Furnace</c> represents one Eurotherm and Camera pair.
/// </summary>
public class Furnace
{
    public int index;
    public string furnaceLabel;
    private Eurotherm Controller;
    private StepperController? stepper;
    private Capture? camera;
    private CancellationTokenSource? _cts;
    private bool isEnabled = false;
    private readonly ChannelReader<FurnaceSet> _in;
    private readonly ChannelWriter<FurnaceState> _out;
    private Profile activeProfile;
    private FurnaceStatus _status;
    private double _setpoint;
    private double _processValue;
    private AlarmStatus _underrange, _overrange, _sensor, _rsp;

    public Furnace(FurnaceInit init, Channel<FurnaceSet> setChannel, Channel<FurnaceState> stateChannel, int _index)
    {
        index = _index;
        _in = setChannel;
        _out = stateChannel;
        furnaceLabel = init.furnaceLabel;
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
        try
        {
            FurnaceSet newSet;
            newSet = await _in.ReadAsync(token);
            while (!token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();

                while (_in.TryRead(out var latest))
                {
                    newSet = latest;
                }

                _processValue = GetProcessValue();
                _status = GetStatus();

                // if (activeProfile.state == ProcessState.Continue)
                // {
                //     activeProfile = newSet.setProfile;   
                // }
                
                activeProfile = newSet.setProfile;

                if (activeProfile != null)
                {
                    switch (newSet.state)
                    {
                        case ProcessState.Continue:
                            activeProfile.Start();
                            break;
                        case ProcessState.Pause:
                            activeProfile.Pause();
                            break;
                        case ProcessState.Stop:
                            activeProfile.Stop();
                            break;
                    }
                }

                if (newSet.manualSetpoint)
                {
                    _setpoint = newSet.setpoint + newSet.trim;
                }
                else if (activeProfile != null)
                {
                    _setpoint = activeProfile.GetSetpoint() + newSet.trim; // TODO integrate camera trim here
                }

                if (newSet.enable)
                {
                    Enable();
                }

                if (_status == FurnaceStatus.Alarm)
                {
                    _overrange = GetAlarm(0);
                    _underrange = GetAlarm(1);
                    _sensor = GetAlarm(2);
                    _rsp = GetAlarm(3);
                }
                else
                {
                    _overrange = AlarmStatus.Off;
                    _underrange = AlarmStatus.Off;
                    _sensor = AlarmStatus.Off;
                    _rsp = AlarmStatus.Off;
                }

                SetSetpoint(_setpoint);
                if (activeProfile != null)
                {
                    FurnaceState newState = new FurnaceState(furnaceLabel, activeProfile.Label, _processValue, _setpoint, _status, _underrange, _overrange, _sensor, _rsp, activeProfile.state.status, (long)activeProfile.OnTimer.ElapsedSeconds);
                    _out.TryWrite(newState);
                }
                else
                {
                    FurnaceState newState = new FurnaceState(furnaceLabel, "None", _processValue, _setpoint, _status, _underrange, _overrange, _sensor, _rsp, ProfileStatus.Stopped, 0);
                    _out.TryWrite(newState);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown path
        }
    }
    
}