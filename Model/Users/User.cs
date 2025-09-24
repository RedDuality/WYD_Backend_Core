using Core.Model.Base;
using Core.Model.Profiles;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Users;

public class User(Account account) : BaseDateEntity
{
    [BsonElement("profiles")]
    [BsonIgnoreIfDefault]
    public List<UserProfile> Profiles { get; set; } = [];

    [BsonElement("accounts")]
    [BsonIgnoreIfDefault]
    public List<Account> Accounts { get; set; } = [account];

    [BsonElement("devices")]
    [BsonIgnoreIfDefault]
    public HashSet<Device> Devices { get; set; } = [];
}

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