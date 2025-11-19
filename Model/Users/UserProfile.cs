using Core.Model.Base;
using Core.Model.Profiles;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Users;

public class UserProfile( User user, Profile profile) : BaseDateEntity
{
    [BsonElement("userId")]
    public ObjectId UserId { get; set; } = user.Id;
    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profile.Id;

    [BsonElement("color")]
    public long Color { get; set; } = 4278190080; //black

    [BsonElement("viewSettings")]
    [BsonIgnoreIfDefault]
    public List<ViewSettings> ViewSettings { get; set; } = [];
}

public class ViewSettings(ObjectId profileId, bool viewConfirmed = false, bool viewShared = false)
{
    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profileId;

    [BsonElement("viewConfirmed")]
    [BsonIgnoreIfDefault]
    public bool ViewConfirmed { get; set; } = viewConfirmed;

    [BsonElement("viewShared")]
    [BsonIgnoreIfDefault]
    public bool ViewShared { get; set; } = viewShared;
}