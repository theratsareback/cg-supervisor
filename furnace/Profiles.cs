namespace furnace;

using Newtonsoft.Json;

public class Segment
{
    public byte Type { get; set; }
    public byte Index { get; set; }
    public uint Duration { get; set; }
    public ushort Endpoint { get; set; }

}
public class Profile
{
    public string? Label { get; set; }
    public List<Segment> Segments { get; set; } = new();
    public Timer OnTimer { get; set; } = new();
    private ulong time = 0;

    public double GetSetpoint()
    {
        time = OnTimer.ElapsedMsForProfile();

        if (Segments.Count == 0)
            return 0;

        double start = 0;       // previous segment endpoint
        ulong cum = 0;          // cumulative elapsed

        foreach (var seg in Segments)
        {
            ulong segDur = seg.Duration;

            // If time falls within this segment
            if (time <= cum + segDur)
            {
                // Handle zero-duration segment as a step to its endpoint
                if (segDur == 0)
                    return seg.Endpoint;

                double tInto = time - (double)cum;            // how far into this segment
                double frac = tInto / segDur;
                double end = seg.Endpoint;
                return start + (end - start) * frac;          // lerp
            }

            // increment to next segment
            cum += segDur;
            start = seg.Endpoint;
        }

        // clamp to last endpoint if all segments are finished
        return start;
    }
}

public class ProfileHandler
{
    private const string FilePath = @"profiles.json";
    public List<Profile> profiles = [];
    public Profile? activeProfile;

    public ProfileHandler()
    {
        if (!File.Exists(FilePath))
        {
            File.WriteAllText(FilePath, "[]"); // if file doesn't exist, make one with an empty list
        }

        profiles = JsonConvert.DeserializeObject<List<Profile>>(File.ReadAllText(FilePath)) ?? [];
        return;
    }

    public void AddProfile(Profile profile)
    {
        profiles.Add(profile);
        string json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }

    public void Select(Profile profile)
    {
        activeProfile = profile;
    }

    public void Start()
    {
        activeProfile?.OnTimer.Start();
    }

    public void Pause()
    {
        activeProfile?.OnTimer.Pause();
    }

    public void Stop()
    {
        activeProfile?.OnTimer.Pause();
        activeProfile?.OnTimer.Reset();
    }
}