using System.Diagnostics.CodeAnalysis;
using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model
{
    [method: SetsRequiredMembers]
    public class EventProfile(ProfileEvent profileEventDocument) : BaseEntity
    {
        [BsonElement("eventId")]
        public required ObjectId EventId { get; set; } = profileEventDocument.EventId;

        [BsonElement("profileId")]
        public required ObjectId ProfileId { get; set; } = profileEventDocument.ProfileId;

        [BsonElement("profileEventId")]
        public required ObjectId ProfileEventId { get; set; } = profileEventDocument.Id;
    }
}
