using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Base
{
    public abstract class BaseCreatedEntity : BaseEntity
    {
        [BsonElement("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
