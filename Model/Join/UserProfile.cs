using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Join;

public class UserProfile(Profile profile)

{
    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profile.Id;

    [BsonElement("color")]
    public long Color { get; set; } = 4278190080; //black
}

