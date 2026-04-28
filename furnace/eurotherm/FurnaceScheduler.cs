namespace furnace.eurotherm;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Channels;
using Newtonsoft.Json;
using OpenCvSharp;

public class FurnaceScheduler : IDisposable
{
    public ConcurrentDictionary<Guid, Furnace> furnaceDict = new();
    public ConcurrentDictionary<Guid, FurnaceSet> setValues = [];
    public ConcurrentDictionary<Guid, FurnaceState> stateValues = [];
    public List<FurnaceInit> _furnacesInit = [];
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _workerCts = [];
    private readonly CancellationToken _globalCt;
    private readonly BoundedChannelOptions opts = new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        };

    public FurnaceScheduler(CancellationToken ct)
    {
        if (!File.Exists(@"furnaces.json"))
        {
            File.WriteAllText(@"furnaces.json", "[]"); // if file doesn't exist, make one with an empty list
        }

        var inits = JsonConvert.DeserializeObject<List<FurnaceInit>>(File.ReadAllText(@"furnaces.json"));
        inits ??= [];
        _furnacesInit = inits;
        _globalCt = ct;

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
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCt);
        _furnacesInit ??= [];
        Guid guid = Guid.NewGuid();
        furnaceDict.AddOrUpdate(guid, (_) => new Furnace(init), (guid, _) => FurnaceUpdateFactory(guid, init));
        if (!_furnacesInit.Contains(init))
        {
            _furnacesInit.Add(init);
            string inits = JsonConvert.SerializeObject(_furnacesInit);
            File.WriteAllText(@"furnaces.json", inits);
        }
        Task.Run(async () => furnaceDict[guid].Run(cts.Token), cts.Token);
        _workerCts.AddOrUpdate(guid, cts, (_, cts) => cts);
    }

    /// <summary>
    /// Method <c>ModifyFurnace</c> is used to modify an existing furnace. 
    /// </summary>
    /// <param name="init">Init struct containing new information for furnace</param>
    public void ModifyFurnace(Guid guid, FurnaceInit init)
    {
        
        furnaceDict.AddOrUpdate(guid, (_) => new Furnace(init), (guid, _) => FurnaceUpdateFactory(guid, init));

        string inits = JsonConvert.SerializeObject(_furnacesInit);
        File.WriteAllText(@"furnaces.json", inits);
    }

    private Furnace FurnaceUpdateFactory(Guid guid, FurnaceInit init)
    {
        CancelWorker(guid);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCt);
        var index = _furnacesInit.IndexOf(furnaceDict[guid].GetInit);
        Furnace furnace = new Furnace(init);

        Task.Run(async () => furnace.Run(cts.Token), cts.Token);
        _workerCts[guid] = cts;
        _furnacesInit[index] = init;

        return furnace;
    }




    public void RemoveFurnace(Guid guid)
    {
        _furnacesInit ??= [];
        CancelWorker(guid);

        List<FurnaceInit> actives = [];
        foreach (FurnaceInit i in _furnacesInit)
        {
            actives.Add(i);
        }
        string inits = JsonConvert.SerializeObject(actives);
        File.WriteAllText(@"furnaces.json", inits);
    }

    /// <summary>
    /// Get dict of inits for existing furnaces
    /// </summary>
    public ConcurrentDictionary<Guid, FurnaceInit> GetInits()
    {
        var dict = new ConcurrentDictionary<Guid, FurnaceInit>();
        foreach (KeyValuePair<Guid, Furnace> furnace in furnaceDict)
        {
            dict.AddOrUpdate(furnace.Key, furnace.Value.GetInit, (_, _) => furnace.Value.GetInit);
        }
        return dict;
    }

    /// <summary>
    /// Push latest FurnaceSet structs to workers
    /// </summary>
    public void Push()
    {
        foreach (var key in setValues.Keys)
        {
            try 
            {
                furnaceDict[key].Push(setValues[key]);
            }
            catch { /* assume new data is for a furnace not yet instantiated */ }
        }
    }

    /// <summary>
    /// Pull latest FurnaceState structs from workers
    /// </summary>
    public void Pull()
    {
        foreach (var key in furnaceDict.Keys)
        {
            var x = furnaceDict[key].Pull();
            if (x != null)
            {
                stateValues[key] = (FurnaceState)x;
            }
        }
    }

    /// <summary>
    /// Cancels a single furnace worker, given the furnace index for that worker
    /// </summary>
    private void CancelWorker(Guid guid)
    {
        _workerCts[guid].Cancel();
    }

    public void Dispose()
    {
        foreach (var cts in _workerCts.Values)
            cts.Dispose();
    }
}