using Core.Model.Enum;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Join;

public class ProfileUser(User user)
{

    [BsonElement("userId")]
    public ObjectId UserId { get; set; } = user.Id;

    [BsonElement("role")]
    public UserRole Role { get; set; } = UserRole.Owner;

    [BsonElement("receivesNotifications")]
    [BsonIgnoreIfDefault]
    public bool ReceivesNotifications { get; set; } = true;

}