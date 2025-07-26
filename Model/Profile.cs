using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model
{
    public class Profile : BaseCreatedEntity
    {
        [BsonElement("tag")]
        public required string Tag { get; set; }

        [BsonElement("name")]
        public required string Name { get; set; }
    }
}
