using Core.Model.Base;
using Core.Model.Join;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model;

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
    public List<Device> Devices { get; set; } = [];
}
