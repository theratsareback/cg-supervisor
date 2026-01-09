using furnace.eurotherm;

namespace furnace;

public enum EventType { NewFurnace, RemoveFurnace, ModifyFurnace, NewProfile, RemoveProfile, ModifyProfile, RequestProfiles, RequestFurnaces, SetFurnaceProfile, AckFurnaceAlarm }

/// <summary>
/// Represents a discrete event and object to be used for the event.
/// These are processed in a queue. No event is ignored.
/// </summary>
public class Event<T>
{
    public EventType type;
    public int index;
    public T? EventObject;

    public Event(EventType _type, T? obj = default)
    {
        type = _type;
        EventObject = obj;
    }

    public void Handle(Coordinator coord)
    {
        switch (type, EventObject)
        {
            case (EventType.NewFurnace, FurnaceInit init):
                coord.NewFurnace(init);
                break;
            case EventType.RemoveFurnace:
                coord.RemoveFurnace(index);
                break;
            case (EventType.ModifyFurnace, FurnaceInit init):
                coord.ModifyFurnace(index, init);
                break;
            case (EventType.NewProfile, ProfileDef profile):
                coord.NewProfile(profile);
                break;
            case (EventType.RemoveProfile, ProfileDef profile):
                coord.RemoveProfile(profile);
                break;
            case (EventType.ModifyProfile, ProfileDef profile):
                coord.ModifyProfile(index, profile);
                break;
            case EventType.RequestProfiles:
                break;
            case EventType.RequestFurnaces:
                break;
            case (EventType.SetFurnaceProfile, ProfileDef profile):
                coord.SetFurnaceProfile(index, profile);
                break;
            case EventType.AckFurnaceAlarm:
                break;
        }
    }
}