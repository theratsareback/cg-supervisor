namespace furnace.profile;
using Newtonsoft.Json;

public class ProfileHandler
{
    private const string FilePath = @"profiles.json";
    public List<ProfileDef> profiles = [];
    public Profile? activeProfile;

    /// <summary>
    /// Instantiates a profileHander object and imports profiles from a .json.
    /// </summary>
    public ProfileHandler()
    {
        if (!File.Exists(FilePath))
        {
            File.WriteAllText(FilePath, "[]"); // if file doesn't exist, make one with an empty list
        }

        profiles = JsonConvert.DeserializeObject<List<ProfileDef>>(File.ReadAllText(FilePath)) ?? [];
        return;
    }

    /// <summary>
    /// Add a profile to the persistent list of profiles.
    /// Does NOT overwrite profiles with the same label.
    /// </summary>
    public void AddProfile(ProfileDef profile)
    {
        profiles.Add(profile);
        string json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }

    /// <summary>
    /// Removes a profile from the persistent list of profiles.
    /// </summary>
    public void RemoveProfile(ProfileDef profile)
    {
        profiles.Remove(profile);
        string json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }

    public void ModifyProfile(int index, ProfileDef profile)
    {
        profiles[index] = profile;
        string json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }
}
