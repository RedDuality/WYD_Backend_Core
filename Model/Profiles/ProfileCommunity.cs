using Core.Model.Base;
using Core.Model.Communities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Profiles;

public class ProfileCommunity(Profile profile, Community community, HashSet<ProfileGroup> groups, Profile? otherProfile) : BaseEntity
{
    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profile.Id;

    [BsonElement("communityId")]
    public ObjectId CommunityId { get; set; } = community.Id;

    [BsonElement("name")]
    [BsonIgnoreIfNull]
    public string? Name { get; set; } = community.Name;

    [BsonElement("type")]
    [BsonIgnoreIfDefault]
    public CommunityType Type { get; set; } = community.Type;

    [BsonElement("communityUpdatedAt")]
    public DateTimeOffset CommunityUpdatedAt { get; set; } = community.UpdatedAt;

    [BsonElement("otherProfileId")]
    [BsonIgnoreIfNull]
    public ObjectId? OtherProfileId { get; set; } = otherProfile?.Id;

    [BsonElement("groups")]
    public HashSet<ProfileGroup> Groups { get; set; } = groups;
}

public class ProfileGroup(Group group, GroupRole role)
{
    [BsonElement("groupId")]
    public ObjectId GroupId { get; set; } = group.Id;

    [BsonElement("name")]
    public string Name { get; set; } = group.Name;

    [BsonElement("isMainGroup")]
    [BsonIgnoreIfDefault]
    public bool IsMainGroup { get; set; } = group.IsMainGroup;

    [BsonElement("role")]
    [BsonIgnoreIfDefault]
    public GroupRole Role { get; set; } = role;
}

