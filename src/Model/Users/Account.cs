using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Users;

public class Account(string uid, string email, SignInType type)
{
    [BsonElement("uid")]
    public string Uid { get; set; } = uid;

    [BsonElement("mail")]
    public string Email { get; set; } = email;

    [BsonElement("signInType")]
    [BsonRepresentation(BsonType.String)]
    public SignInType SignInType { get; set; } = type;

    [BsonElement("importedByProfile")]
    [BsonIgnoreIfNull]
    public ObjectId? ImportedByProfile { get; set; }
}

