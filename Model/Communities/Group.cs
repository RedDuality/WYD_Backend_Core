using Core.Model.Base;
using Core.Model.Communities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Profiles;



public class Group(string name, HashSet<Profile> profiles) : BaseDateEntity
{
    [BsonElement("communityId")]
    public ObjectId? CommmunityId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = name;

    [BsonElement("profiles")]
    public HashSet<Profile> Profiles { get; set; } = profiles;
}

public class GroupProfile(Profile profile, GroupRole role)
{
    [BsonElement("profileId")]
    public ObjectId? ProfileId { get; set; } = profile.Id;

    [BsonElement("role")]
    public GroupRole Role { get; set; } = role;
}