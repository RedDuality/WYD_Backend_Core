using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model
{
    public class Profile(string tag, string name) : BaseCreatedEntity
    {
        [BsonElement("tag")]
        public string Tag { get; set; } = tag;

        [BsonElement("name")]
        public string Name { get; set; } = name;
    }
}
