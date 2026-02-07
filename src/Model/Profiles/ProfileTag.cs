using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Profiles;

public class ProfileTag(Profile p ) : BaseDateEntity
{
    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = p.Id;

    [BsonElement("tag")]
    public string Tag { get; set; } = p.Tag;
}
