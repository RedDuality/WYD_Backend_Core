using Core.Model.Base;
using Core.Model.Enum;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Util
{
    public class ProfileEventWithCorrespondingEvents : BaseDateEntity
    {
        [BsonElement("eventId")]
        public ObjectId EventId { get; set; }

        [BsonElement("profileId")]
        public ObjectId ProfileId { get; set; }

        [BsonElement("confirmed")]
        public bool Confirmed { get; set; }
        [BsonElement("eventUpdatedAt")]
        public DateTimeOffset EventUpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [BsonElement("eventStartTime")]
        public DateTimeOffset EventStartTime { get; set; }
        [BsonElement("eventEndTime")]
        public DateTimeOffset EventEndTime { get; set; }

        [BsonElement("role")]
        public EventRole Role { get; set; }
        public List<Event> Events { get; set; } = [];
    }
}