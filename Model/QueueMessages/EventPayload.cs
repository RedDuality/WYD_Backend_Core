using Core.Model.Events;

namespace Core.Model.QueueMessages;

public enum EventUpdateType
{
    share,

    update,

    confirm,

    decline,
}

public class UpdateEventPayload(Event ev, EventUpdateType type, string? actorId = null)
{
    public Event Event { get; set; } = ev;

    public EventUpdateType Type { get; set; } = type;

    public string? ActorId { get; set; } = actorId;
}
