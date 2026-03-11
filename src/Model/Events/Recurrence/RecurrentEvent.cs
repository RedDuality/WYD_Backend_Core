using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events.Recurrence;

public class RecurrentEvent(
    string title,
    DateTimeOffset startTime,
    DateTimeOffset endTime,
    TimeZoneInfo timeZone,
    string recurrenceRule
    ) : BaseEvent(title, startTime, endTime)
{
    // --- Recurrence ---

    [BsonElement("recurrenceEnd")]
    [BsonIgnoreIfNull]
    public DateTimeOffset? RecurrenceEnd { get; set; }

    [BsonElement("timeZone")]
    [BsonIgnoreIfNull]
    public TimeZoneInfo TimeZone { get; set; } = timeZone;

    [BsonElement("recurrenceRule")]
    public string RecurrenceRule { get; set; } = recurrenceRule;

    // --- imported values ---

    [BsonElement("accountUid")]
    [BsonIgnoreIfNull]
    public string? ImportedAccountUid { get; set; }

    [BsonElement("importedId")]
    [BsonIgnoreIfNull]
    public string? ExternalEventId { get; set; }
}

