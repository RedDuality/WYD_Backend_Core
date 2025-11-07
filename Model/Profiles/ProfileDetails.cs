using Core.Model.Base;
using Core.Model.Communities;
using Core.Model.Users;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Profiles;

public class ProfileDetails(Profile profile) : BaseEntity
{
    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profile.Id;

    [BsonElement("users")]
    public List<ProfileUser> Users { get; set; } = [];
}

public class ProfileUser(User user)
{

    [BsonElement("userId")]
    public ObjectId UserId { get; set; } = user.Id;

    [BsonElement("role")]
    public UserProfileRole Role { get; set; } = UserProfileRole.SuperAdmin;

    [BsonElement("receivesNotifications")]
    [BsonIgnoreIfDefault]
    public bool ReceivesNotifications { get; set; } = true;

}
