using Core.Model.Events;
using Core.Model.Events.Recurrence;

namespace Core.Model.QueueMessages;

public enum EventUpdateType
{
    create,

    share,

    update,

    confirm,

    decline,
}

public class EventPayload(Event ev, EventUpdateType type, string? actorId = null)
{
    public Event Event { get; set; } = ev;

    public EventUpdateType Type { get; set; } = type;

    public string? ActorId { get; set; } = actorId;
}

public class RecurrentEventPayload(RecurrentEvent ev, EventUpdateType type, string? actorId = null)
{
    public RecurrentEvent Event { get; set; } = ev;

    public EventUpdateType Type { get; set; } = type;

    public string? ActorId { get; set; } = actorId;
}
