using Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NRModel
{
    public class ProfileEventDocument(EventDocument ev, ObjectId profileId) : BaseDateEntity
    {
        [BsonElement("eventId")]
        public ObjectId EventId { get; set; } = ev.Id;

        [BsonElement("profileId")]
        public ObjectId ProfileId { get; set; } = profileId;

        [BsonElement("confirmed")]
        public bool Confirmed { get; set; } = false;

        [BsonElement("eventUpdatedAt")]
        public DateTimeOffset EventUpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [BsonElement("eventStartTime")]
        public DateTimeOffset EventStartTime { get; set; } = ev.StartTime.ToUniversalTime();
        [BsonElement("eventEndTime")]
        public DateTimeOffset EventEndTime { get; set; } = ev.EndTime.ToUniversalTime();

        [BsonElement("role")]
        public EventRole Role { get; set; } = EventRole.Viewer;
    }
}
