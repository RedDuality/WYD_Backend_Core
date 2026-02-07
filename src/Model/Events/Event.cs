using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events;

public class Event(string title, DateTimeOffset startTime, DateTimeOffset endTime) : BaseDateEntity
{
    [BsonElement("title")]
    public string Title { get; set; } = title;

    [BsonElement("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    
    [BsonElement("startTime")]
    public DateTimeOffset StartTime { get; set; } = startTime.ToUniversalTime();

    [BsonElement("endTime")]
    public DateTimeOffset EndTime { get; set; } = endTime.ToUniversalTime();

    [BsonElement("TotalProfiles")]
    [BsonIgnoreIfDefault]
    public int TotalProfilesMinusOne { get; set; } = 0;

    [BsonElement("TotalConfirmed")]
    [BsonIgnoreIfDefault]
    public int TotalConfirmedMinusOne { get; set; } = 0;
}

