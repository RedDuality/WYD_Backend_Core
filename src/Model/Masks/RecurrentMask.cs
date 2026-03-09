using Core.Model.Base;
using Core.Model.Events.Recurrence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Masks;

public class RecurrentMask : BaseDateEntity
{
    [BsonElement("profileId")]
    public ObjectId ProfileId;

    [BsonElement("startTime")]
    public DateTimeOffset StartTime;

    [BsonElement("endTime")]
    public DateTimeOffset EndTime;

    [BsonElement("title")]
    [BsonIgnoreIfNull]
    public string? Title;

    [BsonElement("eventId")]
    [BsonIgnoreIfNull]
    public ObjectId? EventId;

    // --- Recurrence ---

    [BsonElement("recurrenceEnd")]
    [BsonIgnoreIfNull]
    public DateTimeOffset? RecurrenceEnd { get; set; }

    [BsonElement("timeZone")]
    [BsonIgnoreIfNull]
    public TimeZoneInfo TimeZone { get; set; }

    [BsonElement("recurrenceRule")]
    [BsonIgnoreIfNull]
    public string RecurrenceRule { get; set; }




    public RecurrentMask(
        ObjectId profileId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string? title,
        DateTimeOffset? recurrenceEnd,
        TimeZoneInfo timezone,
        string recurrenceRule)
    {
        ProfileId = profileId;
        StartTime = startTime.ToUniversalTime();
        EndTime = endTime.ToUniversalTime();
        Title = title;
        RecurrenceEnd = recurrenceEnd;
        TimeZone = timezone;
        RecurrenceRule = recurrenceRule;
    }

    public RecurrentMask(ObjectId profileId, RecurrentEvent ev, string? title = null)
    {
        ProfileId = profileId;
        EventId = ev.Id;
        StartTime = ev.StartTime.ToUniversalTime();
        EndTime = ev.EndTime.ToUniversalTime();
        Title = title ?? ev.Title;
        RecurrenceEnd = ev.RecurrenceEnd;
        TimeZone = ev.TimeZone;
        RecurrenceRule = ev.RecurrenceRule;
    }

}

