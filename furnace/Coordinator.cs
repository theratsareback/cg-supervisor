using furnace.eurotherm;
using furnace.profile;

namespace furnace;


public class Coordinator
{
    private FurnaceScheduler furnaceScheduler;
    private ProfileHandler profileHandler;

    public Coordinator()
    {
        furnaceScheduler = new FurnaceScheduler();
        profileHandler = new ProfileHandler();
    }
    
    public void NewFurnace(FurnaceInit init)
    {
        furnaceScheduler.NewFurnace(init);
    }

    public void RemoveFurnace(int index)
    {
        furnaceScheduler.RemoveFurnace(index);
    }
    
    public void ModifyFurnace(int index, FurnaceInit init)
    {
        furnaceScheduler.ModifyFurnace(index, init);
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

    /// <summary>
    /// Sends the profile defs currently in the profile handler to the frontend via GRPC
    /// </summary>
    public void RequestProfiles()
    {
        // profileHandler.profiles;
        // TODO send by GRPC
    }

    /// <summary>
    /// Sends the furnace inits currently in the furnace scheduler to the frontend via GRPC
    /// </summary>
    public void RequestFurnaces()
    {
        List<FurnaceInit> inits = furnaceScheduler.GetInits();
        // TODO send by GRPC
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

    public void SetSetValues(int index, FurnaceSet newSet)
    {
        furnaceScheduler.setValues[index] = newSet;
    }

    public void Update()
    {
        furnaceScheduler.Pull();
        furnaceScheduler.Push();
    }
}