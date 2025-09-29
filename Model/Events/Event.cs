using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events;

public class Event : BaseDateEntity
{
    [BsonElement("title")]
    public required string Title { get; set; } = "Untitled";

    [BsonElement("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [BsonElement("startTime")]
    public required DateTimeOffset StartTime { get; set; }

    [BsonElement("endTime")]
    public required DateTimeOffset EndTime { get; set; }

    [BsonElement("TotalProfiles")]
    [BsonIgnoreIfDefault]
    public int TotalProfilesMinusOne { get; set; } = 0;

    [BsonElement("TotalConfirmed")]
    [BsonIgnoreIfDefault]
    public int TotalConfirmedMinusOne { get; set; } = 0;
}

