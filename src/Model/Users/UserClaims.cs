using Core.Model.Base;
using Core.Model.Profiles;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Users;

public class UserClaims(User user, Profile profile) : BaseDateEntity
{
    [BsonElement("userId")]
    public ObjectId UserId { get; set; } = user.Id;

    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profile.Id;

    [BsonElement("claims")]
    public HashSet<UserClaim> Claims { get; set; } = [];
}

public class UserClaim(UserClaimType claim)
{
    [BsonElement("claim")]
    [BsonRepresentation(BsonType.String)]
    public UserClaimType Claim { get; set; } = claim;

    [BsonElement("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

}