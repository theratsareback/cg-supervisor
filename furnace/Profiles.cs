namespace furnace;

using Newtonsoft.Json;

public class Segment
{
    public byte Type { get; set; }
    // Type 1 is ramp, 2 is dwell, 3 is pause
    public byte Index { get; set; }
    public uint Duration { get; set; }
    public double Endpoint { get; set; }

}

public class Profile
{
    public string? Label { get; set; }
    public List<Segment> Segments { get; set; } = new();
    public Timer OnTimer { get; set; } = new();

    [JsonIgnore] public byte type = 0;
    [JsonIgnore] public bool segmentPause = false;

    private byte _currentIndex = 0;
    private double _out = 0;

    /// <summary>
    /// Returns the setpoint for the furnace.
    /// </summary>
    public double GetSetpoint()
    {
        uint _now = OnTimer.ElapsedMsForProfile();

        if (Segments.Count == 0) return 0;
        ulong cum = 0;          // cumulative elapsed

        foreach (var seg in Segments)
        {
            ulong segDur = seg.Duration;

            // If time falls within this segment
            if (_now <= cum + segDur)
            {
                type = seg.Type;
                if (_currentIndex != 0)
                {
                    _out = Segments[_currentIndex - 1].Endpoint;   // start from previous endpoint if there is one
                }

                if (type == 3) return _out; OnTimer.Pause(); segmentPause = true; // stay at last endpoint if paused

                double tInto = _now - (double)cum;            // how far into this segment
                double frac = tInto / segDur;
                double end = seg.Endpoint;
                return _out + (end - _out) * frac;          // lerp
            }

            // increment to next segment
            cum += segDur;
            _currentIndex++;
        }
        // clamp to last endpoint if all segments are finished
        return _out;
    }

    /// <summary>
    /// Skip current segment of profile.
    /// Currently only works for the "pause" segment type.
    /// Use profileHandler.Start() to resume from a manual pause.
    /// </summary>
    public void Skip()
    {
        if (_currentIndex >= Segments.Count || type != 3) return;
        // avoid skipping non-pause segments, would need to add remaining time to ontimer to do that
        _currentIndex++;
        OnTimer.Start();
        segmentPause = false;
    }
}

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

    /// <summary>
    /// Set a profile as the active profile for this furnace.
    /// </summary>
    public void Select(Profile profile)
    {
        activeProfile = profile;
    }

    /// <summary>
    /// Start (or resume) active profile timebase
    /// Use activeProfile.Skip() to resume from a program pause
    /// </summary>
    public void Start()
    {
        activeProfile?.OnTimer.Start();
    }

    /// <summary>
    /// Pauses current profile timebase
    /// </summary>
    public void Pause()
    {
        activeProfile?.OnTimer.Pause();
    }

    /// <summary>
    /// Pauses and resets current profile timebase
    /// </summary>
    public void Stop()
    {
        activeProfile?.OnTimer.Pause();
        activeProfile?.OnTimer.Reset();
    }
}