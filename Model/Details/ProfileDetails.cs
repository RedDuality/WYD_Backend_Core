
using Core.Model.Base;
using Core.Model.Join;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Details;

public class ProfileDetails(Profile profile) : BaseEntity
{
    [BsonElement("profileId")]
    public ObjectId ProfileId { get; set; } = profile.Id;

    [BsonElement("users")]
    public List<ProfileUser> Users { get; set; } = [];
}