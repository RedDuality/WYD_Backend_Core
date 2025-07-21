using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NRModel
{
    [method: SetsRequiredMembers]
    public class EventProfileDocument(ProfileEventDocument profileEventDocument) : BaseEntity
    {
        [BsonElement("eventId")]
        public required ObjectId EventId { get; set; } = profileEventDocument.EventId;

        [BsonElement("profileId")]
        public required ObjectId ProfileId { get; set; } = profileEventDocument.ProfileId;

        [BsonElement("profileEventId")]
        public required ObjectId ProfileEventId { get; set; } = profileEventDocument.Id;
    }
}
