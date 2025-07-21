using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace NRModel
{
    public class ProfileDocument : BaseCreatedEntity
    {
        [BsonElement("tag")]
        public required string Tag { get; set; }

        [BsonElement("name")]
        public required string Name { get; set; }
    }
}
