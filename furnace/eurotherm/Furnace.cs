namespace furnace.eurotherm;

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using furnace.profile;
using furnace.stepper;
using furnace.camera;
using Microsoft.VisualBasic;

/// <summary>
/// Class <c>Furnace</c> represents one Eurotherm, camera, and stepper driver.
/// </summary>
public class Furnace
{
    public string furnaceLabel;
    public ProcessState state;
    private Eurotherm Controller;
    private StepperController? stepper;
    private Capture? camera;
    private CancellationTokenSource? _cts;
    private bool isEnabled = false;
    private ChannelReader<FurnaceSet> _in;
    private ChannelWriter<FurnaceState> _out;
    private Channel<ProcessState> _statechannel;
    private Channel<Profile> _profilechannel;
    private Profile? activeProfile;
    private FurnaceStatus _status;
    private double _setpoint;
    private double _processValue;
    private AlarmStatus _underrange, _overrange, _sensor, _rsp;
    private object _alarmLock;
    private readonly BoundedChannelOptions opts = new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };

    public Furnace(FurnaceInit init, Channel<FurnaceSet> setChannel, Channel<FurnaceState> stateChannel)
    {
        _in = setChannel;
        _out = stateChannel;
        furnaceLabel = init.furnaceLabel;
        Controller = new Eurotherm(init.eurothermIp, init.eurothermPort);
        Controller.Connect();
        _statechannel = Channel.CreateBounded<ProcessState>(opts);
        _profilechannel = Channel.CreateBounded<Profile>(opts);

        //camera = new Capture(camIp, camPort);
        //camera.Start();
        //TODO MOTORS
        return;
    
    }

    public void ModifyFurnace(FurnaceInit init, Channel<FurnaceSet> setChannel, Channel<FurnaceState> stateChannel)
    {
        _in = setChannel;
        _out = stateChannel;
        furnaceLabel = init.furnaceLabel;
        Controller = new Eurotherm(init.eurothermIp, init.eurothermPort);
        Controller.Connect();
        //camera = new Capture(camIp, camPort);
        //camera.Start();
        //TODO MOTORS
    }

    public void SetSetpoint(double setpoint)
    {
        Controller.Heater.SP(setpoint);
    }

    /// <summary>
    /// Enable heating element output. Safe to call if already enabled.
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
    /// Disable heating element output. Safe to call if already disabled.
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
    /// </returns>
    private AlarmStatus GetAlarm(byte alarmIndex)
    {
        AlarmStatus status = Controller.alarms[alarmIndex].GetStatus();
        return status;
    }

    //TODO make function to safely read alarm values from the async thread

    public void SetProfile(ProfileDef def)
    {
        if (activeProfile == null || activeProfile.state.Status == ProfileStatus.Stopped)
        {
            _profilechannel.Writer.TryWrite(new(def));
        }
    }

    public void SetState(ProcessState newState)
    {
        _statechannel.Writer.TryWrite(newState);
    }

    public async Task Run(CancellationToken token)
    {
        try
        {
            FurnaceSet newSet;
            newSet = await _in.ReadAsync(token);
            state = await _statechannel.Reader.ReadAsync(token);
            activeProfile = await _profilechannel.Reader.ReadAsync(token);

            while (!token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();

                while (_in.TryRead(out var latest))
                {
                    newSet = latest;
                }

                _processValue = GetProcessValue();
                _status = GetStatus();

                if (activeProfile != null)
                {
                    switch (state)
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
                    lock (_alarmLock)
                    {
                        _overrange = GetAlarm(0);
                        _underrange = GetAlarm(1);
                        _sensor = GetAlarm(2);
                        _rsp = GetAlarm(3);
                    }
                }
                else
                {
                    lock (_alarmLock)
                    {
                        _overrange = AlarmStatus.Off;
                        _underrange = AlarmStatus.Off;
                        _sensor = AlarmStatus.Off;
                        _rsp = AlarmStatus.Off;
                    }
                }

                SetSetpoint(_setpoint);
                if (activeProfile != null)
                {
                    FurnaceState newState = new FurnaceState(furnaceLabel, activeProfile.Label, _processValue, _setpoint, _status, _underrange, _overrange, _sensor, _rsp, activeProfile.state.Status, (long)activeProfile.OnTimer.ElapsedSeconds);
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