using Core.Model.Base;
using Core.Model.Profiles;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Communities;



public class Group(Community community, string name, HashSet<GroupProfile> profiles, bool? mainGroup = false) : BaseDateEntity
{
    [BsonElement("communityId")]
    public ObjectId? CommmunityId { get; set; } = community.Id;

    [BsonElement("name")]
    public string Name { get; set; } = name;

    [BsonElement("isMainGroup")]
    [BsonIgnoreIfDefault]
    public bool IsMainGroup = mainGroup ?? false;

    [BsonElement("profiles")]
    public HashSet<GroupProfile> Profiles { get; set; } = profiles;
}

public class GroupProfile(Profile profile, GroupRole? role)
{
    [BsonElement("profileId")]
    public ObjectId? ProfileId { get; set; } = profile.Id;

    [BsonElement("role")]
    [BsonIgnoreIfDefault]
    public GroupRole Role { get; set; } = role ?? GroupRole.Viewer;
}