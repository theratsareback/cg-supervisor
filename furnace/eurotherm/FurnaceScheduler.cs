namespace furnace.eurotherm;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Channels;
using Newtonsoft.Json;
using OpenCvSharp;

public class FurnaceScheduler : IDisposable
{
    // public List<FurnaceSet> setValues = [];
    // public List<FurnaceState> stateValues = [];
    public List<FurnaceInit> _furnacesInit = [];
    public readonly List<Furnace> furnaces = [];
    // private readonly List<Channel<FurnaceSet>> _setChannels = [];
    // private readonly List<Channel<FurnaceState>> _stateChannels = [];
    private FurnaceBus _bus;
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
        _bus = new FurnaceBus();
        if (!File.Exists(@"furnaces.json"))
        {
            File.WriteAllText(@"furnaces.json", "[]"); // if file doesn't exist, make one with an empty list
        }

        var inits = JsonConvert.DeserializeObject<List<FurnaceInit>>(File.ReadAllText(@"furnaces.json"));
        inits ??= [];
        _furnacesInit = inits;

        int i = 0;
        foreach (FurnaceInit init in _furnacesInit)
        {
            NewFurnace(init);
            i++;
        }
    }

    /// <summary>
    /// Method <c>NewFurnace</c> is used to add a new furnace to the system. 
    /// </summary>
    /// <param name="init">Init struct containing information for new furnace</param>
    public void NewFurnace(FurnaceInit init)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
        // var setChannel = Channel.CreateBounded<FurnaceSet>(opts);
        // var stateChannel = Channel.CreateBounded<FurnaceState>(opts);
        // _setChannels.Add(setChannel);
        // _stateChannels.Add(stateChannel);

        _furnacesInit ??= [];
        Furnace furnace = new Furnace(init);
        furnaces.Add(furnace);
        if (!_furnacesInit.Contains(init))
        {
            _furnacesInit.Add(init);
            List<FurnaceInit> actives = [];
            foreach (FurnaceInit i in _furnacesInit)
            {
                if (i.index >= 0)
                {
                    actives.Add(i);
                }
            }
            string inits = JsonConvert.SerializeObject(actives);
            File.WriteAllText(@"furnaces.json", inits);
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
        CancelWorker(index);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);

        Furnace furnace = new Furnace(init);
        furnaces[index] = furnace;

        _workerTasks[index] = furnace.Run(cts.Token);
        _workerCts[index] = cts;
        _furnacesInit[index] = init;

        string inits = JsonConvert.SerializeObject(_furnacesInit);
        File.WriteAllText(@"furnaces.json", inits);
    }
    
    public void RemoveFurnace(int index)
    {
        // _furnacesInit ??= [];
        // _furnacesInit[index].index = -1;
        // CancelWorker(index);
        // stateValues[index]._active = false;

        // List<FurnaceInit> actives = [];
        // foreach (FurnaceInit i in _furnacesInit)
        // {
        //     if (i.index >= 0)
        //     {
        //         actives.Add(i);
        //     }
        // }
        // string inits = JsonConvert.SerializeObject(actives);
        // File.WriteAllText(@"furnaces.json", inits);
    }

    /// <summary>
    /// Get list of inits for existing furnaces
    /// </summary>
    public List<FurnaceInit> GetInits()
    {
        List<FurnaceInit>? inits = JsonConvert.DeserializeObject<List<FurnaceInit>>(File.ReadAllText(@"furnaces.json"));
        inits ??= [];
        return inits;
    }

    // /// <summary>
    // /// Push latest FurnaceSet structs to workers
    // /// </summary>
    // public void Push()
    // {
    //     for (int i = 0; i < _setChannels.Count; i++)
    //         _setChannels[i].Writer.TryWrite(setValues[i]);
    // }

    // /// <summary>
    // /// Pull latest FurnaceState structs from workers
    // /// </summary>
    // public void Pull()
    // {
    //     for (int i = 0; i < _stateChannels.Count; i++)
    //     {
    //         var reader = _stateChannels[i].Reader;

    //         FurnaceState last = default;
    //         bool sawAny = false;

    //         while (reader.TryRead(out FurnaceState v))
    //         {
    //             last = v;
    //             sawAny = true;
    //         }

    //         if (sawAny)
    //             stateValues[i] = last;
    //     }
    // }

    /// <summary>
    /// Cancels a single furnace worker, given the furnace index for that worker
    /// </summary>
    private void CancelWorker(int index, bool closeChannels = true)
    {
        if ((uint)index >= (uint)furnaces.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _workerCts[index].Cancel();

        if (closeChannels)
        {
            _bus.TryRemove(furnaces[index].guid); //TODO this is stupid just remove furnaces by guid not index
        }
    }

    /// <summary>
    /// Cancels all running furnace workers
    /// </summary>
    private void CancelAll(bool closeChannels = true)
    {
        _globalCts.Cancel();

        if (closeChannels)
        {
            for (int i = 0; i < furnaces.Count; i++)
            {
                _bus.TryRemove(furnaces[i].guid);
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