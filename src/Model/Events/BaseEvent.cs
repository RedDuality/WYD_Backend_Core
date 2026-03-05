using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events;

public abstract class BaseEvent(
    string title,
    DateTimeOffset startTime,
    DateTimeOffset endTime
    ) : BaseDateEntity
{
    [BsonElement("title")]
    public string Title { get; set; } = title;

    [BsonElement("startTime")]
    public DateTimeOffset StartTime { get; set; } = startTime.ToUniversalTime();

    [BsonElement("endTime")]
    public DateTimeOffset EndTime { get; set; } = endTime.ToUniversalTime();

    [BsonElement("isAllDay")]
    [BsonIgnoreIfDefault]
    public bool IsAllDay { get; set; } = false;
}
