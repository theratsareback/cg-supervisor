using furnace.eurotherm;
using furnace.profile;
using furnace.grpc;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace furnace;

using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;

public sealed class Coordinator : IHostedService, IDisposable
{
    private FurnaceScheduler? furnaceScheduler;
    private ProfileHandler? profileHandler;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        furnaceScheduler = new FurnaceScheduler(cancellationToken);
        profileHandler = new ProfileHandler();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        furnaceScheduler?.Dispose();
        furnaceScheduler = null;
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
    public void SetFurnaceProfile(int index, ProfileDef profileDef)
    {
        furnaceScheduler.furnaces[index].SetProfile(profileDef);
    }

    /// <summary>
    /// Pause, stop, or resume following of profile
    /// </summary>
    public void SetState(int index, ProcessState state)
    {
        furnaceScheduler.furnaces[index].SetState(state);
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
        EventType type = (EventType)_event.Type;
        int index = (int)_event.Index;
        var EventObject = JsonConvert.DeserializeObject(_event.Payload);
        switch (type, EventObject)
        {
            case (EventType.NewFurnace, FurnaceInit init):
                NewFurnace(init);
                return "";

            case (EventType.RemoveFurnace, Guid guid):
                RemoveFurnace(guid);
                return "";

            case (EventType.ModifyFurnace, KeyValuePair<FurnaceInit, Guid>(FurnaceInit init, Guid guid)):
                ModifyFurnace(guid, init);
                return "";

            case (EventType.NewProfile, ProfileDef profile):
                NewProfile(profile);
                return "";

            case (EventType.RemoveProfile, ProfileDef profile):
                RemoveProfile(profile);
                return "";

            case (EventType.ModifyProfile, ProfileDef profile):
                ModifyProfile(index, profile);
                return "";

            case (EventType.RequestProfiles, var _):
                return JsonConvert.SerializeObject(RequestProfiles());

            case (EventType.RequestFurnaces, var _):
                return JsonConvert.SerializeObject(furnaceScheduler.GetInits());

            case (EventType.SetFurnaceProfile, ProfileDef profile):
                SetFurnaceProfile(index, profile);
                return "";

            case (EventType.AckFurnaceAlarm, var _):
                furnaceScheduler.furnaces[index].AckAlarms();
                return "";
        }
        return "";
    }
}