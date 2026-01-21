using furnace.eurotherm;
using Newtonsoft.Json;

namespace furnace;

public enum EventType { NewFurnace, RemoveFurnace, ModifyFurnace, NewProfile, RemoveProfile, ModifyProfile, RequestProfiles, RequestFurnaces, SetFurnaceProfile, AckFurnaceAlarm }

/// <summary>
/// Represents a discrete event and object to be used for the event.
/// These are processed in a queue. No event is ignored.
/// </summary>
public class EventDispatch
{
    public EventType type;
    public int index;
    public string EventObject;

    public EventDispatch(EventType _type, string? obj = default)
    {
        type = _type;
        EventObject = obj;
    }

    public void Handle(Coordinator coord)
    {
        // switch (type)
        // {
        //     case (EventType.NewFurnace):
        //         FurnaceInit? init = JsonConvert.DeserializeObject<FurnaceInit>(EventObject);
        //         coord.NewFurnace(init);
        //         break;
        //     case EventType.RemoveFurnace:
        //         string guid = EventObject;
        //         coord.RemoveFurnace(guid);
        //         break;
        //     case (EventType.ModifyFurnace):
        //         coord.ModifyFurnace(guid, init);
        //         break;
        //     case (EventType.NewProfile, ProfileDef profile):
        //         coord.NewProfile(profile);
        //         break;
        //     case (EventType.RemoveProfile, ProfileDef profile):
        //         coord.RemoveProfile(profile);
        //         break;
        //     case (EventType.ModifyProfile, ProfileDef profile):
        //         coord.ModifyProfile(index, profile);
        //         break;
        //     case EventType.RequestProfiles:
        //         break;
        //     case EventType.RequestFurnaces:
        //         break;
        //     case (EventType.SetFurnaceProfile, ProfileDef profile):
        //         coord.SetFurnaceProfile(index, profile);
        //         break;
        //     case EventType.AckFurnaceAlarm:
        //         break;
        // }
    }
}

public static class EventMapper
{
    public static EventHandler<T> ToDomain<T>(furnace.grpc.Event proto)
    {
        var type = (EventType)proto.Type;
        var index = checked((int)proto.Index);

        T? obj = default;
        if (!string.IsNullOrEmpty(proto.Payload))
        {
            obj = JsonConvert.DeserializeObject<T>(proto.Payload);
        }

        //return new EventHandler<T>(type, obj) { index = index };
    }
}