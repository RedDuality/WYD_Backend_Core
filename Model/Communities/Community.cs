using Core.Model.Base;
using Core.Model.Profiles;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Communities;

public class Community(string name, Profile owner, CommunityType? type) : BaseDateEntity
{
    [BsonElement("name")]
    public string Name { get; set; } = name;

    [BsonElement("type")]
    [BsonIgnoreIfDefault]
    public CommunityType Type { get; set; } = type ?? CommunityType.Personal;

    [BsonElement("ownerId")]
    public ObjectId OwnerId { get; set; } = owner.Id;

    
    [BsonElement("mainGroupId")]
    public ObjectId MainGroupId { get; set; }

    [BsonElement("groups")]
    public List<ObjectId> Groups { get; set; } = [];
}

