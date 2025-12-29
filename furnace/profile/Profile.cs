namespace furnace.profile;
using furnace;

using Newtonsoft.Json;

public readonly record struct ProfileState(
    int CurrentIndex,
    double LastOut,
    ProfileStatus Status,
    byte CurrentType
);

public enum CoordinatorEffect { None, Pause, Resume }

public readonly record struct EvalOutput(
    double Setpoint,
    ProfileState NextState,
    CoordinatorEffect Effect
);

/// <summary>
/// This is all ai-generated but seems to work, start debugging from here if there are any problems with profile logic
/// </summary>
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
            var empty = state with { CurrentIndex = 0, LastOut = 0, Status = ProfileStatus.Paused, CurrentType = 0 };
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
                        Status: ProfileStatus.Paused,
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
                    var nextPause = new ProfileState(i, lastOut, ProfileStatus.Paused, seg.Type);
                    return new EvalOutput(lastOut, nextPause, CoordinatorEffect.Pause);
                }

                double tInto = (double)nowMs - (double)cum;
                double frac = tInto / (double)segDur;
                double setpoint = lastOut + (seg.Endpoint - lastOut) * frac;

                var nextRun = new ProfileState(i, lastOut, ProfileStatus.Running, seg.Type);
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
            Status: ProfileStatus.Stopped,
            CurrentType: segments[^1].Type
        );
        return new EvalOutput(lastOut, endState, CoordinatorEffect.None);
    }
}


public sealed partial class Profile
{
    public string Label { get; init; }
    public List<Segment> Segments { get; } = new();
    public Timer OnTimer { get; set; } = new();

    [JsonIgnore] public byte Type => state.CurrentType;

    public ProfileState state = new(CurrentIndex: 0, LastOut: 0, Status: ProfileStatus.Paused, CurrentType: 0);

    public Profile(ProfileDef def)
    {
        Segments = def.Segments;
        Label = def.Label;
    }

    /// <summary>
    /// Get current setpoint according to profile
    /// </summary>
    public double GetSetpoint()
    {
        var now = OnTimer.ElapsedMsForProfile();
        var output = ProfileMath.Evaluate(Segments, now, state);

        switch (output.Effect)
        {
            case CoordinatorEffect.Pause:
                Pause();
                break;
            case CoordinatorEffect.Resume:
                Start();
                break;
            case CoordinatorEffect.None:
            default:
                break;
        }

        state = output.NextState;
        return output.Setpoint;
    }

    /// <summary>
    /// Reset profile state machine before starting a fresh run.
    /// </summary>
    public void Reset(double initialOut = 0)
    {
        state = new ProfileState(0, Segments[0].Endpoint, ProfileStatus.Stopped, 0);
    }

    /// <summary>
    /// Start (or resume) active profile timebase.
    /// </summary>
    public void Start()
    {
        if (state.CurrentType == 3)
        {
            Console.WriteLine("starting");
            int next = System.Math.Min(state.CurrentIndex + 1, Segments.Count - 1);
            double lastOut = (next > 0) ? Segments[next - 1].Endpoint : state.LastOut;

            state = state with
            {
                CurrentIndex = next,
                LastOut = lastOut,
                Status = ProfileStatus.Running,
                CurrentType = Segments[next].Type
            };
            OnTimer.Start();
        }
        else if (state.Status != ProfileStatus.Running)
        {
            Console.WriteLine("starting from non-segment pause");
            state = state with
            {
                CurrentIndex = state.CurrentIndex,
                LastOut = state.LastOut,
                Status = ProfileStatus.Running,
                CurrentType = state.CurrentType
            };
            OnTimer.Start();
        }
        // does nothing if not paused or on a pause segment
    }

    /// <summary>
    /// Pauses current profile timebase
    /// </summary>
    public void Pause()
    {   
        if (state.Status != ProfileStatus.Paused)
        {
            OnTimer.Pause();

            state = state with
            {
                CurrentIndex = state.CurrentIndex,
                LastOut = state.LastOut,
                Status = ProfileStatus.Paused,
                CurrentType = state.CurrentType
            };
        }
    }

    /// <summary>
    /// Pauses and resets current profile timebase
    /// </summary>
    public void Stop()
    {   
        if (state.Status != ProfileStatus.Stopped)
        {
            OnTimer.Pause();
            OnTimer.Reset();
            Reset();
        }
    }
}