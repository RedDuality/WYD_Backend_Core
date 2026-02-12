using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events;

public class EventDetails(Event ev) : BaseDateEntity
{
    [BsonElement("eventId")]
    public ObjectId EventId { get; set; } = ev.Id;

    [BsonElement("Description")]
    [BsonIgnoreIfDefault]
    public string? Description { get; set; }

    [BsonElement("totalImages")]
    [BsonIgnoreIfDefault]
    public long TotalImages { get; set; } = 0;
}