using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events;

public abstract class BaseEvent : BaseDateEntity
{
    [BsonElement("title")]
    public string Title { get; set; }

    [BsonElement("startTime")]
    public DateTimeOffset StartTime { get; set; }

    [BsonElement("endTime")]
    public DateTimeOffset EndTime { get; set; }

    [BsonElement("isAllDay")]
    [BsonIgnoreIfDefault]
    public bool IsAllDay { get; set; } = false;

    public BaseEvent(
        string title,
        DateTimeOffset startTime,
        DateTimeOffset endTime)
    {
        if (endTime < startTime)
            throw new ArgumentException("EndTime cannot be earlier than StartTime.");

        Title = title;
        StartTime = startTime;
        EndTime = endTime;
    }
}
