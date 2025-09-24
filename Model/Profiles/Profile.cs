using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Profiles;

public class Profile(string tag, string name) : BaseDateEntity
{
    [BsonElement("tag")]
    public string Tag { get; set; } = tag;

    [BsonElement("name")]
    public string Name { get; set; } = name;
}

