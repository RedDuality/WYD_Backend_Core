using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NRModel
{
    public abstract class BaseDateEntity : BaseCreatedEntity
    {
        [BsonElement("updatedAt")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
