using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Users;

public class User(Account account) : BaseDateEntity
{
    [BsonElement("mainProfileId")]
    public ObjectId MainProfileId { get; set; }

    [BsonElement("profileIds")]
    public HashSet<ObjectId> ProfileIds { get; set; } = [];

    [BsonElement("accounts")]
    [BsonIgnoreIfDefault]
    public List<Account> Accounts { get; set; } = [account];

    [BsonElement("devices")]
    [BsonIgnoreIfDefault]
    public HashSet<Device> Devices { get; set; } = [];
}