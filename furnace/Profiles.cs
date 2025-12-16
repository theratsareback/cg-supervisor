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

public readonly record struct ProfileState(
    int CurrentIndex,
    double LastOut,
    bool IsPaused,
    byte CurrentType
);

public enum CoordinatorEffect { None, Pause, Resume }

public readonly record struct EvalOutput(
    double Setpoint,
    ProfileState NextState,
    CoordinatorEffect Effect
);

public static class ProfileMath
{
    public static EvalOutput Evaluate(
        IReadOnlyList<Segment> segments,
        ulong nowMs,
        ProfileState state
    )
    {
        if (segments.Count == 0)
        {
            var empty = state with { CurrentIndex = 0, LastOut = 0, IsPaused = false, CurrentType = 0 };
            return new EvalOutput(0, empty, CoordinatorEffect.None);
        }

        // Start from the remembered index instead of rescanning from 0
        int i = state.CurrentIndex;
        if (i < 0) i = 0;
        if (i >= segments.Count) i = segments.Count - 1;

        // Compute the segment-start time (cum) at i
        ulong cum = 0;
        for (int k = 0; k < i; k++) cum += segments[k].Duration;

        // Baseline output at the boundary before segment i
        double lastOut = (i > 0) ? segments[i - 1].Endpoint : state.LastOut;

        // Walk forward from i, consuming zero-duration events exactly once
        while (i < segments.Count)
        {
            var seg = segments[i];
            ulong segDur = seg.Duration;

            // --- Zero-duration handling (instantaneous events) ---
            if (segDur == 0)
            {
                if (seg.Type == 3)
                {
                    // Zero-duration PAUSE: pause immediately at this boundary and *stay* on i.
                    var next = new ProfileState(
                        CurrentIndex: i,
                        LastOut: lastOut,
                        IsPaused: true,
                        CurrentType: seg.Type
                    );
                    return new EvalOutput(lastOut, next, CoordinatorEffect.Pause);
                }

                // Zero-duration non-pause: apply the jump, advance index once, and keep going.
                lastOut = seg.Endpoint;
                i++;                     // <-- consumes it so we won't process it again
                // cum unchanged (no time elapsed)
                continue;
            }

            // --- Timed segment ---
            ulong segEnd = cum + segDur;

            if (nowMs <= segEnd)
            {
                if (seg.Type == 3)
                {
                    // Defensive: timed pause holds and requests a pause
                    var nextPause = new ProfileState(i, lastOut, true, seg.Type);
                    return new EvalOutput(lastOut, nextPause, CoordinatorEffect.Pause);
                }

                double tInto = (double)nowMs - (double)cum;
                double frac = tInto / (double)segDur;
                double setpoint = lastOut + (seg.Endpoint - lastOut) * frac;

                var nextRun = new ProfileState(i, lastOut, false, seg.Type);
                return new EvalOutput(setpoint, nextRun, CoordinatorEffect.None);
            }

            // Past this segment: move to the next timed one
            cum = segEnd;
            lastOut = seg.Endpoint;
            i++;
        }

        // Past the end: clamp to final endpoint
        var endState = new ProfileState(
            CurrentIndex: segments.Count - 1,
            LastOut: lastOut,
            IsPaused: false,
            CurrentType: segments[^1].Type
        );
        return new EvalOutput(lastOut, endState, CoordinatorEffect.None);
    }
}


/// <summary>
/// State-machine coordinator 
/// </summary>
public sealed class Profile
{
    public string? Label { get; init; }
    public List<Segment> Segments { get; } = new();
    public Timer OnTimer { get; set; } = new();

    // Public flags (optional) if you want visibility similar to your original class:
    [JsonIgnore] public byte Type => _state.CurrentType;
    [JsonIgnore] public bool SegmentPause => _state.IsPaused;

    private ProfileState _state = new(CurrentIndex: 0, LastOut: 0, IsPaused: false, CurrentType: 0);

    /// <summary>
    /// Reads the clock, calls the PURE evaluator, applies any side-effects, and commits NextState.
    /// </summary>
    public double GetSetpoint()
    {
        var now = OnTimer.ElapsedMsForProfile();
        var output = ProfileMath.Evaluate(Segments, now, _state);

        // Apply requested side-effects
        switch (output.Effect)
        {
            case CoordinatorEffect.Pause:
                OnTimer.Pause();
                break;
            case CoordinatorEffect.Resume:
                OnTimer.Start();
                break;
            case CoordinatorEffect.None:
            default:
                break;
        }

        // Commit next state
        _state = output.NextState;
        return output.Setpoint;
    }

    /// <summary>
    /// Skip the current segment. If on a pause segment, resume timer and move to the next.
    /// Ignores other segment types.
    /// </summary>
    public void Skip()
    {
        if (Segments.Count == 0) return;

        if (_state.CurrentType == 3)
        {
            int next = System.Math.Min(_state.CurrentIndex + 1, Segments.Count - 1);
            // Set LastOut to the segment we just left (i), which ends at its Endpoint
            double lastOut = (next > 0) ? Segments[next - 1].Endpoint : _state.LastOut;

            _state = _state with
            {
                CurrentIndex = next,
                LastOut = lastOut,
                IsPaused = false,
                CurrentType = Segments[next].Type
            };

            OnTimer.Start();
        }
    }

    /// <summary>
    /// Reset profile state machine before starting a fresh run.
    /// </summary>
    public void Reset(double initialOut = 0)
    {
        _state = new ProfileState(0, initialOut, false, 0);
        OnTimer.Start();
    }
}

public class ProfileHandler
{
    private const string FilePath = @"profiles.json";
    public List<Profile> profiles = [];
    public Profile? activeProfile;
    public bool isRunning;
    public bool stopped;

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
        if (!isRunning)
        {
            activeProfile?.OnTimer.Start();
            isRunning = true;
            stopped = false;
        }
    }

    /// <summary>
    /// Pauses current profile timebase
    /// </summary>
    public void Pause()
    {   
        if (isRunning)
        {
            activeProfile?.OnTimer.Pause();
            isRunning = false;
            stopped = false;
        }
    }

    /// <summary>
    /// Pauses and resets current profile timebase
    /// </summary>
    public void Stop()
    {
        if (stopped)
        {  
            activeProfile?.OnTimer.Pause();
            activeProfile?.OnTimer.Reset();
            isRunning = false;
            stopped = true;
        }
    }
}
