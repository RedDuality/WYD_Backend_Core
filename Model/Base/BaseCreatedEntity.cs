using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NRModel
{
    public abstract class BaseCreatedEntity : BaseEntity
    {
        [BsonElement("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
