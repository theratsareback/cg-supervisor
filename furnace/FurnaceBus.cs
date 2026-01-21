using System.Collections.Concurrent;
using System.Threading.Channels;

namespace furnace;

public sealed class FurnaceBus
{
    private sealed class FurnaceChannels
    {
        public required Channel<FurnaceSet> A { get; init; }
        public required Channel<FurnaceState> B { get; init; }
    }

    private readonly ConcurrentDictionary<string, FurnaceChannels> _workers = new();

    private readonly UnboundedChannelOptions _aOpts;
    private readonly UnboundedChannelOptions _bOpts;

    public FurnaceBus(
        UnboundedChannelOptions? aOpts = null,
        UnboundedChannelOptions? bOpts = null)
    {
        _aOpts = aOpts ?? new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        _bOpts = bOpts ?? new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };
    }

    private FurnaceChannels Get(string workerId) =>
        _workers.GetOrAdd(workerId, _ => new FurnaceChannels
        {
            A = Channel.CreateUnbounded<FurnaceSet>(_aOpts),
            B = Channel.CreateUnbounded<FurnaceState>(_bOpts),
        });

    public ChannelReader<FurnaceSet> SetReader(string workerId) => Get(workerId).A.Reader;
    public ChannelWriter<FurnaceSet> SetWriter(string workerId) => Get(workerId).A.Writer;

    public ChannelReader<FurnaceState> StateReader(string workerId) => Get(workerId).B.Reader;
    public ChannelWriter<FurnaceState> StateWriter(string workerId) => Get(workerId).B.Writer;

    public bool TryRemove(string workerId)
    {
        if (_workers.TryRemove(workerId, out var ch))
        {
            ch.A.Writer.TryComplete();
            ch.B.Writer.TryComplete();
            return true;
        }
        return false;
    }
}
