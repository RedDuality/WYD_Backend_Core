using Core.Services.Util;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events.Recurrence;

public class RecurrentEvent : BaseEvent {
    // --- Recurrence ---

    [BsonElement("recurrenceEnd")]
    public DateTimeOffset RecurrenceEnd { get; set; }

    [BsonElement("timeZone")]
    [BsonIgnoreIfNull]
    public TimeZoneInfo TimeZone { get; set; }

    [BsonElement("recurrenceRule")]
    public string RecurrenceRule { get; set; }

    // --- imported values ---

    [BsonElement("accountUid")]
    [BsonIgnoreIfNull]
    public string? ImportedAccountUid { get; set; }

    [BsonElement("importedId")]
    [BsonIgnoreIfNull]
    public string? ExternalEventId { get; set; }

    public RecurrentEvent(string title,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        TimeZoneInfo timeZone,
        string recurrenceRule
    ) : base(title, startTime, endTime) {
        var validRule = RecurrenceService.GetValidRule(recurrenceRule);

        var recurrenceEnd = RecurrenceService.ExtractRecurrenceEnd(validRule, timeZone);

        if (recurrenceEnd < startTime)
            throw new ArgumentException(
                $"Recurrence UNTIL ({recurrenceEnd}) cannot be earlier than StartTime ({startTime}).",
                nameof(recurrenceRule)
            );

        TimeZone = timeZone;
        RecurrenceRule = validRule;
        RecurrenceEnd = recurrenceEnd;
    }
}

