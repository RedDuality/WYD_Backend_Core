using Core.Model.Base;
using Core.Model.Events.Recurrence;
using Core.Model.Profiles;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Util.EventsQuery;

public class ProfileRecurrentEventWithCorrespondingEvents : BaseDateEntity
{
    [BsonElement("eventId")]
    public ObjectId EventId { get; set; }

    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; }

    [BsonElement("confirmed")]
    public bool Confirmed { get; set; }

    [BsonElement("role")]
    public EventRole Role { get; set; }

    // list because mongodb does not know it can only be one event
    public List<RecurrentEvent> Events { get; set; } = [];
}
