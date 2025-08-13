using Core.Model.Base;
using Core.Model.Join;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model;

public class User(Account account) : BaseDateEntity
{
    [BsonElement("profiles")]
    public List<UserProfile> Profiles { get; set; } = [];

    [BsonElement("accounts")]
    public List<Account> Accounts { get; set; } = [account];
}
