namespace furnace.eurotherm;

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using furnace.profile;
using furnace.stepper;
using furnace.camera;
using furnace.diameter;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// Class <c>Furnace</c> represents one Eurotherm, camera, and stepper driver.
/// </summary>
public class Furnace
{
    private DiameterControl _diameterControl;
    public string furnaceLabel;
    public FurnaceInit GetInit;
    private ProfileStatus state;
    private Eurotherm Controller;
    private StepperController? stepper;
    private Capture? camera;
    private CancellationTokenSource? _cts;
    private bool isEnabled = false;
    private Channel<FurnaceSet> _in;
    private Channel<FurnaceState> _out;
    private Channel<ProfileStatus> _statechannel;
    private Channel<Profile> _profilechannel;
    private Channel<double> _trimchannel;
    private Profile? activeProfile;
    private FurnaceStatus _status;
    private double _setpoint;
    private double _lastSetpoint;
    private double _trim;
    private double _processValue;
    private AlarmStatus _underrange, _overrange, _sensor, _rsp;
    private object _alarmLock = new();
    private readonly BoundedChannelOptions _inOpts = new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
    private readonly BoundedChannelOptions _outOpts = new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = true
        };

    public Furnace(FurnaceInit init, DiameterControl diameterControl)
    {
        _in = Channel.CreateBounded<FurnaceSet>(_inOpts);
        _out = Channel.CreateBounded<FurnaceState>(_outOpts);
        furnaceLabel = init.furnaceLabel;
        GetInit = init;
        Controller = new Eurotherm(init.eurothermIp, init.eurothermPort);
        _statechannel = Channel.CreateBounded<ProfileStatus>(_inOpts);
        _profilechannel = Channel.CreateBounded<Profile>(_inOpts);
        _trimchannel = Channel.CreateBounded<double>(_inOpts);
        _diameterControl = diameterControl;

        //camera = new Capture(camIp, camPort);
        //camera.Start();
        //TODO MOTORS
        return;
    
    }

    public void Push(FurnaceSet set)
    {
        _in.Writer.TryWrite(set);
    }

    public FurnaceState? Pull()
    {
        if (_out.Reader.TryRead(out var x))
        {
            return x;
        }
        return null;
    }

    public void SetSetpoint(double setpoint)
    {
        Controller.Heater.SP(setpoint);
    }

    public void SetTrim(double trim)
    {
        _ = _trimchannel.Writer.WriteAsync(trim);
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

    public void Toggle()
    {
        if (isEnabled)
        {
            Controller.Heater.Disable();
            isEnabled = false;
        }
        else
        {
            Controller.Heater.Enable();
            isEnabled = true;
        }
    }

    public float GetProcessValue()
    {
        float pv = Controller.Heater.PV();
        return pv;
    }

    /// <summary>
    /// Checks the status of the furnace's Eurotherm.
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

    public void SetProfileStatus(ProfileStatus newStatus)
    {
        _statechannel.Writer.TryWrite(newStatus);
    }
    
    /// <summary>
    /// TODO remove and make individual acknowledgements
    /// </summary>
    public void AckAlarms()
    {
        foreach (Alarm alarm in Controller.alarms)
        {
            alarm.Acknowledge();
        }
    }

    public void Seek(TimeSpan timeSpan)
    {
        activeProfile.OnTimer.Seek(timeSpan);
    }

    public async Task Run(CancellationToken token)
    {
        try
        {
            FurnaceSet newSet;
            //newSet = await _in.Reader.ReadAsync(token);
            newSet = default;
            state = ProfileStatus.Stopped;
            //activeProfile = await _profilechannel.Reader.ReadAsync(token);

            while (!token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();

                if (_in.Reader.TryRead(out var set))
                {
                    newSet = set;
                }
                if (_profilechannel.Reader.TryRead(out var newProfile))
                {
                    activeProfile = newProfile;
                }
                if (_trimchannel.Reader.TryRead(out var zrim))
                {
                    _trim = zrim;
                }
                if (_statechannel.Reader.TryRead(out var zoop))
                {
                    state = zoop;
                }

                _processValue = GetProcessValue();
                _status = GetStatus();

                if (activeProfile != null)
                {
                    switch (state)
                    {
                        case ProfileStatus.Running:
                            activeProfile.Start();
                            break;
                        case ProfileStatus.Paused:
                            activeProfile.Pause();
                            break;
                        case ProfileStatus.Stopped:
                            activeProfile.Stop();
                            break;
                    }
                }

                if (newSet.manualSetpoint)
                {
                    _setpoint = newSet.setpoint + _trim;
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

                if (activeProfile != null)
                {
                    _setpoint = activeProfile.GetSetpoint() + _trim + _diameterControl.GetTrim();
                    SetSetpoint(_setpoint);
                }

                if (activeProfile != null)
                {
                    FurnaceState newState = new FurnaceState
                    {
                        furnaceLabel = furnaceLabel,
                        processValue = _processValue,
                        setpoint = _setpoint,
                        furnaceStatus = _status,
                        underrangeAlarm = _underrange,
                        overrangeAlarm = _overrange,
                        sensorBreak = _sensor,
                        rspFailure = _rsp,
                        profileStatus = activeProfile.state.Status,
                        time_s = (long)activeProfile.OnTimer.ElapsedSeconds
                    };

                    _out.Writer.TryWrite(newState);
                }
                else
                {
                    FurnaceState newState = new FurnaceState
                    {
                        furnaceLabel = furnaceLabel,
                        processValue = _processValue,
                        setpoint = _setpoint,
                        furnaceStatus = _status,
                        underrangeAlarm = _underrange,
                        overrangeAlarm = _overrange,
                        sensorBreak = _sensor,
                        rspFailure = _rsp,
                        profileStatus = ProfileStatus.Stopped,
                        time_s = 0
                    };
                    
                    _out.Writer.TryWrite(newState);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown path
        }
    }

}