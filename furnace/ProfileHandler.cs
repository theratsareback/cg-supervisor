namespace furnace;
using Newtonsoft.Json;

public class ProfileHandler
{
    private const string FilePath = @"profiles.json";
    public List<Profile> profiles = [];
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

        profiles = JsonConvert.DeserializeObject<List<Profile>>(File.ReadAllText(FilePath)) ?? [];
        return;
    }

    /// <summary>
    /// Add a profile to the persistent list of profiles.
    /// Does NOT overwrite profiles with the same label.
    /// </summary>
    public void AddProfile(Profile profile)
    {
        profiles.Add(profile);
        string json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }

    /// <summary>
    /// Removes a profile from the persistent list of profiles.
    /// </summary>
    public void RemoveProfile(Profile profile)
    {
        profiles.Remove(profile);
        string json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }

    public void ModifyProfile(Profile profile)
    {
        foreach (Profile oldprofile in profiles)
        {
            if (oldprofile.profileIndex == profile.profileIndex)
            {
                RemoveProfile(oldprofile);
                AddProfile(profile);
            }
        }
    }
}
