namespace furnace;
using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using Newtonsoft.Json;

public class FurnaceScheduler : IDisposable
{
    public List<FurnaceSet> setValues = [];
    public List<FurnaceState> stateValues = [];
    public List<FurnaceInit>? _furnacesInit = [];
    public readonly List<Furnace> furnaces = [];
    private readonly List<Channel<FurnaceSet>> _setChannels = [];
    private readonly List<Channel<FurnaceState>> _stateChannels = [];
    private readonly List<Task> _workerTasks = [];
    private readonly List<CancellationTokenSource> _workerCts = [];
    private readonly CancellationTokenSource _globalCts = new CancellationTokenSource();
    private readonly BoundedChannelOptions opts = new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        };

    public FurnaceScheduler()
    {
        if (!File.Exists(@"furnaces.json"))
        {
            File.WriteAllText(@"furnaces.json", "[]"); // if file doesn't exist, make one with an empty list
        }

        _furnacesInit = JsonConvert.DeserializeObject<List<FurnaceInit>>(File.ReadAllText(@"furnaces.json"));
        _furnacesInit ??= [];

        int i = 0;
        foreach (FurnaceInit init in _furnacesInit)
        {
            NewFurnace(init, i);
            i++;
        }
    }

    /// <summary>
    /// Method <c>NewFurnace</c> is used to add a new furnace to the system. 
    /// </summary>
    /// <param name="init">Init struct containing information for new furnace</param>
    public void NewFurnace(FurnaceInit init, int index = -1)
    {
        if (index < 0)
        {
            index = furnaces.Count;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
        var setChannel = Channel.CreateBounded<FurnaceSet>(opts);
        var stateChannel = Channel.CreateBounded<FurnaceState>(opts);
        _setChannels.Add(setChannel);
        _stateChannels.Add(stateChannel);
        setValues.Add(default);
        stateValues.Add(new FurnaceState());

        _furnacesInit ??= [];
        Furnace furnace = new Furnace(init, setChannel, stateChannel, index);
        furnaces.Add(furnace);
        if (!_furnacesInit.Contains(init))
        {
            _furnacesInit.Add(init);
            // TODO save changes in JSON and ignore furnaces with index -1
        }

        _workerTasks.Add(furnace.Run(cts.Token));
        _workerCts.Add(cts);
    }

    /// <summary>
    /// Method <c>ModifyFurnace</c> is used to modify an existing furnace. 
    /// </summary>
    /// <param name="init">Init struct containing new information for furnace</param>
    public void ModifyFurnace(int index, FurnaceInit init)
    {
        //TODO implement
    }
    
    public void RemoveFurnace(int index)
    {
        _furnacesInit ??= [];
        _furnacesInit[index].index = -1;
        CancelWorker(index);
        stateValues[index]._active = false;
    }

    /// <summary>
    /// Push latest FurnaceSet structs to workers
    /// </summary>
    public void Push()
    {
        for (int i = 0; i < _setChannels.Count; i++)
            _setChannels[i].Writer.TryWrite(setValues[i]);
    }

    /// <summary>
    /// Pull latest FurnaceState structs from workers
    /// </summary>
    public void Pull()
    {
        for (int i = 0; i < _stateChannels.Count; i++)
        {
            var reader = _stateChannels[i].Reader;

            FurnaceState last = default;
            bool sawAny = false;

            while (reader.TryRead(out FurnaceState v))
            {
                last = v;
                sawAny = true;
            }

            if (sawAny)
                stateValues[i] = last;
        }
    }

    /// <summary>
    /// Cancels a single furnace worker, given the furnace index for that worker
    /// </summary>
    public void CancelWorker(int index, bool closeChannels = true)
    {
        if ((uint)index >= (uint)furnaces.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _workerCts[index].Cancel();

        if (closeChannels)
        {
            _stateChannels[index].Writer.TryComplete();
            _setChannels[index].Writer.TryComplete();
        }
    }

    /// <summary>
    /// Cancels all running furnace workers
    /// </summary>
    public void CancelAll(bool closeChannels = true)
    {
        _globalCts.Cancel();

        if (closeChannels)
        {
            for (int i = 0; i < furnaces.Count; i++)
            {
                _setChannels[i].Writer.TryComplete();
                _stateChannels[i].Writer.TryComplete();
            }
        }
    }

    public void Dispose()
    {
        CancelAll(closeChannels: true);

        foreach (var cts in _workerCts)
            cts.Dispose();

        _globalCts.Dispose();
    }
}