using Core.Model.Base;
using Core.Model.Events.Recurrence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Util;

public class ProfileRecurrentEventWithCorrespondingEvents : BaseDateEntity
{
    [BsonElement("eventId")]
    public ObjectId EventId { get; set; }

    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; }

    // list because mongodb does not know it can only be one event
    public List<RecurrentEvent> Events { get; set; } = [];
}
