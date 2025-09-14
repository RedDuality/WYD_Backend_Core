using Core.Model.Base;
using Core.Model.Enum;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model;

public class Media : BaseDateEntity
{
    [BsonElement("parentId")]
    public required ObjectId ParentId { get; set; }

    // profileId
    [BsonElement("ownerId")]
    public required ObjectId OwnerId { get; set; }

    //contains the "."
    [BsonElement("extension")]
    public required string Extension { get; set; }

    [BsonElement("name")]
    [BsonIgnoreIfDefault]
    public string? Name { get; set; }

    [BsonElement("creationDate")]
    public DateTimeOffset CreationDate { get; set; }

    [BsonElement("status")]
    [BsonIgnoreIfDefault]
    public MediaStatus Status { get; set; } = MediaStatus.Created;

    [BsonElement("visibility")]
    [BsonIgnoreIfDefault]
    public MediaVisibility Visibility { get; set; } = MediaVisibility.Private;
}
