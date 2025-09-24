using Core.Model.Base;
using Core.Model.Profiles;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Communities;

public class Community(string name, Profile owner, ObjectId groupId, CommunityType type = CommunityType.Personal) : BaseDateEntity
{
    [BsonElement("name")]
    public string Name { get; set; } = name;

    [BsonElement("type")]
    [BsonIgnoreIfDefault]
    public CommunityType Type { get; set; } = type;

    [BsonElement("ownerId")]
    public ObjectId OwnerId { get; set; } = owner.Id;

    [BsonElement("groups")]
    public List<ObjectId> Groups { get; set; } = [groupId];
}

