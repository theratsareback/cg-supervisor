namespace furnace.profile;
using System;
using System.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;

public sealed class Timer
{
    [JsonIgnore] private readonly Stopwatch _sw = new();

    public long AccumulatedTicks { get; private set; }

    [JsonIgnore] public bool IsRunning => _sw.IsRunning;

    private object _sync = new();
    private TimeSpan _offset = new TimeSpan(0);

    public void Start()
    {
        if (_sw.IsRunning) return;
        _sw.Start();
    }

    public void Pause()
    {
        if (!_sw.IsRunning) return;
        _sw.Stop();
        AccumulatedTicks += _sw.Elapsed.Ticks;
        _sw.Reset();
        
    }

    public void Reset()
    {
        _sw.Reset();
        AccumulatedTicks = 0;
        
    }

    private long Count()
    {
        lock (_sync)
        {
            return AccumulatedTicks;
        }
    }

    public void Seek(TimeSpan timeSpan)
    {
        _offset = _offset.Add(timeSpan);
    }

    [JsonIgnore]
    public TimeSpan Elapsed => TimeSpan.FromTicks(AccumulatedTicks + (_sw.IsRunning ? _sw.Elapsed.Ticks : 0)).Add(_offset);

    public long ElapsedMilliseconds => (long)Elapsed.TotalMilliseconds;
    public double ElapsedSeconds => Elapsed.TotalSeconds;

    public uint ElapsedMsForProfile() => (uint)Math.Min(uint.MaxValue, Elapsed.TotalMilliseconds);
    public uint ElapsedSecForProfile() => (uint)Math.Min(uint.MaxValue, Elapsed.TotalSeconds);
}