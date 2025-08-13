using Core.Model.Enum;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Join;

public class UserProfile(Profile profile, UserRole userRole = UserRole.Viewer, bool mainProfile = false)

{
    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profile.Id;

    [BsonElement("role")]
    public UserRole Role { get; set; } = userRole;

    [BsonElement("color")]
    public long Color { get; set; } = 4278190080; //black

    [BsonElement("MainProfile")]
    [BsonIgnoreIfDefault]
    public bool MainProfile { get; set; } = mainProfile;
}

