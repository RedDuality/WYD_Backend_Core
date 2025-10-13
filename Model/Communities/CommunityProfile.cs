using Core.Model.Base;
using Core.Model.Profiles;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Communities;

public class CommunityProfile(ProfileCommunity profileCommunity) : BaseEntity
{
    [BsonElement("communityId")]
    public ObjectId CommunityId { get; set; } = profileCommunity.CommunityId;

    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profileCommunity.ProfileId;
}

