using Core.Model.Events;

namespace Core.Model.QueueMessages;

public enum UpdateType
{
    share,

    update,

    confirm,

    decline,
}

public class UpdateEventPayload(Event ev, UpdateType type, string? actorId = null)
{
    public Event Event { get; set; } = ev;

    public UpdateType Type { get; set; } = type;

    public string? ActorId { get; set; } = actorId;
}
