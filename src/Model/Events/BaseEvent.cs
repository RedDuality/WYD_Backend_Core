using Core.Model.Base;
using Ical.Net.DataTypes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events;

public abstract class BaseEvent : BaseDateEntity {
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
        DateTimeOffset endTime) {
        if (endTime < startTime)
            throw new ArgumentException("EndTime cannot be earlier than StartTime.");

        Title = title;
        StartTime = startTime;
        EndTime = endTime;
    }

    public TimeSpan GetTimeSpan() {
        return EndTime - StartTime;
    }

    public Duration GetDuration() {
        return Duration.FromTimeSpanExact(GetTimeSpan());
    }
}
