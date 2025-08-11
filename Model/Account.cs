using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model;

public class Account(string uid, string email)
{
    [BsonElement("uid")]
    public string Uid { get; set; } = uid;

    [BsonElement("mail")]
    public string Email { get; set; } = email;

}

