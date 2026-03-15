using Core.Model.Base;
using Core.Model.Events.Recurrence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Profiles;

public class ProfileRecurrentEvent(RecurrentEvent ev, ObjectId profileId) : BaseDateEntity
{
    [BsonElement("eventId")]
    public ObjectId EventId { get; set; } = ev.Id;

    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profileId;

    [BsonElement("confirmed")]
    [BsonIgnoreIfDefault]
    public bool Confirmed { get; set; }

    [BsonElement("eventUpdatedAt")]
    public DateTimeOffset EventUpdatedAt { get; set; } = ev.UpdatedAt;

    [BsonElement("recurrenceStart")]
    public DateTimeOffset RecurrenceStart { get; set; } = ev.StartTime.ToUniversalTime();

    [BsonElement("recurrenceEnd")]
    public DateTimeOffset? RecurrenceEnd { get; set; } = ev.RecurrenceEnd?.ToUniversalTime();

}

