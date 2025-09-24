using Core.Model.Base;
using Core.Model.Communities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Profiles;

public class ProfileCommunity(Profile profile, Community community, HashSet<ProfileGroup> profileGroups) : BaseEntity
{
    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profile.Id;

    [BsonElement("communityId")]
    public ObjectId CommunityId { get; set; } = community.Id;

    [BsonElement("name")]
    public string Name { get; set; } = community.Name;

    [BsonElement("type")]
    [BsonIgnoreIfDefault]
    public CommunityType Type { get; set; } = community.Type;

    [BsonElement("communityUpdatedAt")]
    public DateTimeOffset CommunityUpdatedAt { get; set; } = community.UpdatedAt;

    [BsonElement("communityUpdatedAt")]
    public HashSet<ProfileGroup> Groups = profileGroups;
}

public class ProfileGroup(Group group)
{
    [BsonElement("groupId")]
    public ObjectId GroupId { get; set; } = group.Id;

    [BsonElement("name")]
    public string Name { get; set; } = group.Name;

    [BsonElement("role")]
    [BsonIgnoreIfDefault]
    public GroupRole Role { get; set; } = GroupRole.Viewer;
}

