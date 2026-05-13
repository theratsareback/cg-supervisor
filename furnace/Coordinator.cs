using furnace.eurotherm;
using furnace.profile;
using furnace.grpc;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace furnace;

using System.Collections.Concurrent;
using furnace.diameter;
using furnace.grpc;
using Microsoft.Extensions.Hosting;

public sealed class Coordinator : IHostedService, IDisposable
{
    private FurnaceScheduler furnaceScheduler;
    private ProfileHandler profileHandler;
    private DiameterControl diameterControl;
    private StepperGrpcClient stepperClient;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        furnaceScheduler = new FurnaceScheduler(cancellationToken, diameterControl);
        profileHandler = new ProfileHandler();
        diameterControl = new DiameterControl(4, 6.92, 15, 45, 0.1);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        furnaceScheduler.Dispose();
    }
    
    public void NewFurnace(FurnaceInit init)
    {
        furnaceScheduler.NewFurnace(init);
    }

    public void RemoveFurnace(Guid guid)
    {
        furnaceScheduler.RemoveFurnace(guid);
    }
    
    public void ModifyFurnace(Guid guid, FurnaceInit init)
    {
        furnaceScheduler.ModifyFurnace(guid, init);
    }

    public void NewProfile(ProfileDef profile)
    {
        profileHandler.AddProfile(profile);
    }

    public void RemoveProfile(ProfileDef profile)
    {
        profileHandler.RemoveProfile(profile);
    }

    public void ModifyProfile(int index, ProfileDef profile)
    {
        profileHandler.ModifyProfile(index, profile);
    }

    public List<ProfileDef> RequestProfiles()
    {
        return profileHandler.profiles;
    }
    
    /// <summary>
    /// Sets a new active profile for a furnace. Only has an effect if the furnace is stopped or if there is no active profile.
    /// </summary>
    public void SetFurnaceProfile(Guid guid, ProfileDef profileDef)
    {
        furnaceScheduler.furnaceDict[guid].SetProfile(profileDef);
    }

    /// <summary>
    /// Pause, stop, or resume following of profile
    /// </summary>
    public void SetProfileStatus(Guid guid, ProfileStatus status)
    {
        furnaceScheduler.furnaceDict[guid].SetProfileStatus(status);
    }

    /// <summary>
    /// Returns the profile defs loaded into the profile handler
    /// </summary>
    public List<ProfileDef> GetProfileDefs()
    {
        return profileHandler.profiles;
    }

    public void SetSetValue(Guid target, FurnaceSet newSet)
    {
        furnaceScheduler.setValues[target] = newSet;
    }

    public void Update()
    {
        furnaceScheduler.Pull();
        furnaceScheduler.Push();
    }

    public ConcurrentDictionary<Guid, FurnaceState> GetStateValues()
    {
        furnaceScheduler.Pull();
        return furnaceScheduler.stateValues;
    }

    public void SetSetValues(ConcurrentDictionary<Guid, FurnaceSet> newSets)
    {
        furnaceScheduler.setValues = newSets;
        furnaceScheduler.Push();
    }

    public string Handle(Event _event)
    {   
        switch ((EventType)_event.Type)
        {
            case EventType.NewFurnace:
                {
                    FurnaceInit init = JsonConvert.DeserializeObject<FurnaceInit>(_event.Payload);
                    NewFurnace(init);
                    return "";
                }

            case EventType.RemoveFurnace:
                {
                    Guid guid = JsonConvert.DeserializeObject<Guid>(_event.Payload);
                    RemoveFurnace(guid);
                    return "";
                }

            case EventType.ModifyFurnace:
                {
                    KeyValuePair<Guid, FurnaceInit> pair = JsonConvert.DeserializeObject<KeyValuePair<Guid, FurnaceInit>>(_event.Payload);
                    ModifyFurnace(pair.Key, pair.Value);
                    return "";
                }

            case EventType.NewProfile:
                {
                    ProfileDef profile = JsonConvert.DeserializeObject<ProfileDef>(_event.Payload);
                    NewProfile(profile);
                    return "";
                }

            case EventType.RemoveProfile:
                {
                    ProfileDef profile = JsonConvert.DeserializeObject<ProfileDef>(_event.Payload);
                    RemoveProfile(profile);
                    return "";
                }

            case EventType.ModifyProfile:
                {
                    KeyValuePair<int, ProfileDef> pair = JsonConvert.DeserializeObject<KeyValuePair<int, ProfileDef>>(_event.Payload);
                    ModifyProfile(pair.Key, pair.Value);
                    return "";
                }

            case EventType.RequestProfiles:
                return JsonConvert.SerializeObject(RequestProfiles());

            case EventType.RequestFurnaces:
                return JsonConvert.SerializeObject(furnaceScheduler.GetInits());

            case EventType.SetProfileStatus:
                {
                    KeyValuePair<Guid, ProfileStatus> pair = JsonConvert.DeserializeObject<KeyValuePair<Guid, ProfileStatus>>(_event.Payload);
                    SetProfileStatus(pair.Key, pair.Value);
                    return "";
                }

            case EventType.SetFurnaceProfile:
                {
                    KeyValuePair<Guid, ProfileDef> pair = JsonConvert.DeserializeObject<KeyValuePair<Guid, ProfileDef>>(_event.Payload);
                    SetFurnaceProfile(pair.Key, pair.Value);
                    return "";
                }

            case EventType.AckFurnaceAlarm:
                {
                    Guid guid = JsonConvert.DeserializeObject<Guid>(_event.Payload);
                    furnaceScheduler.furnaceDict[guid].AckAlarms();
                    return "";
                }

            case EventType.SetSteppers:
                {
                    StepperBuf stepperBuf = JsonConvert.DeserializeObject<StepperBuf>(_event.Payload);
                    stepperClient.SetStepper(stepperBuf.StepperId, stepperBuf.Frequency, stepperBuf.Direction);
                    return "";
                }

            case EventType.Enable:
                {
                    Guid guid = JsonConvert.DeserializeObject<Guid>(_event.Payload);
                    furnaceScheduler.furnaceDict[guid].Toggle();
                    return "";
                }

            case EventType.SetSetpoint:
                {
                    KeyValuePair<Guid, Double> pair = JsonConvert.DeserializeObject<KeyValuePair<Guid, Double>>(_event.Payload);
                    furnaceScheduler.furnaceDict[pair.Key].SetSetpoint(pair.Value);
                    return "";
                }

            case EventType.StartDiameterControl:
                diameterControl.Start();
                return "";

            case EventType.SetManTrim:
                {
                    KeyValuePair<Guid, Double> pair = JsonConvert.DeserializeObject<KeyValuePair<Guid, Double>>(_event.Payload);
                    furnaceScheduler.furnaceDict[pair.Key].SetTrim(pair.Value);
                    return "";
                }

            case EventType.SetGains:
                {
                    double kp = JsonConvert.DeserializeObject<double>(_event.Payload);
                    diameterControl.SetGain(kp);
                    return "";
                }

            case EventType.SeekTime:
                {
                    KeyValuePair<Guid, int> pair = JsonConvert.DeserializeObject<KeyValuePair<Guid, int>>(_event.Payload);
                    TimeSpan time = new TimeSpan(0, 0, 0, 0, pair.Value);
                    furnaceScheduler.furnaceDict[pair.Key].Seek(time);
                    return "";
                }
        }
        return "";
    }

    public void ProcessMassData(uint[] timeArray, double[] massArray)
    {
        diameterControl.AddData(timeArray, massArray);
    }
}