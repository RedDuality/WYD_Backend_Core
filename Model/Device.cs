using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model;

public class Device(string platform, string fcmToken) : BaseCreatedEntity
{
    [BsonElement("platform")]
    public string Platform { get; set; } = platform;

    [BsonElement("fcmToken")]
    public string FcmToken { get; set; } = fcmToken;
}

