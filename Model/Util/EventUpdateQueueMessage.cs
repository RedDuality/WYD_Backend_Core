
namespace Core.Model.Util
{
    public class EventUpdateQueueMessage(string eventId)
    {
        public string EventId { get; set; } = eventId;
        public EventUpdateType Type { get; set; } = EventUpdateType.Simple;
        public DateTimeOffset? UpdatedAt { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
    }
}