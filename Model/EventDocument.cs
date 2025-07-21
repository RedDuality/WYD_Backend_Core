using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NRModel
{
    public class Event : BaseDateEntity
    {
        [BsonElement("title")]
        public required string Title { get; set; } = "Untitled";

        [BsonElement("description")]
        public string? Description { get; set; }

        [BsonElement("timestamp")]
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        [BsonElement("startTime")]
        public required DateTimeOffset StartTime { get; set; }

        [BsonElement("endTime")]
        public required DateTimeOffset EndTime { get; set; }
    }
}
