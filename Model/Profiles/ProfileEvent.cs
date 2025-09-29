using Core.Model.Base;
using Core.Model.Events;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Profiles;

public class ProfileEvent(Event ev, ObjectId profileId) : BaseDateEntity
{
    [BsonElement("eventId")]
    public ObjectId EventId { get; set; } = ev.Id;

    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profileId;

    [BsonElement("confirmed")]
    public bool Confirmed { get; set; } = false;

    [BsonElement("eventUpdatedAt")]
    public DateTimeOffset EventUpdatedAt { get; set; } = ev.UpdatedAt;

    [BsonElement("eventStartTime")]
    public DateTimeOffset EventStartTime { get; set; } = ev.StartTime.ToUniversalTime();
    [BsonElement("eventEndTime")]
    public DateTimeOffset EventEndTime { get; set; } = ev.EndTime.ToUniversalTime();

    [BsonElement("role")]
    public EventRole Role { get; set; } = EventRole.Viewer;

}

