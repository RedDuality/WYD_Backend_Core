using Core.Model.Base;
using Core.Model.Profiles;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events.Recurrence;

public class RecurrentEventProfile(ProfileRecurrentEvent profileEvent) : BaseEntity
{
    [BsonElement("eventId")]
    public ObjectId EventId { get; set; } = profileEvent.EventId;

    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profileEvent.ProfileId;
}

