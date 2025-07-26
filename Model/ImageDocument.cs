using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NRModel
{
    public class ImageDocument : BaseDateEntity
    {
        [BsonElement("eventId")]
        public required ObjectId EventId { get; set; }

        [BsonElement("finisched")]
        public bool Finished { get; set; } = false;
        
        [BsonElement("extension")]
        public required string Extension { get; set; }
    }
}
