using Core.Model.Base;
using Core.Model.Events;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Masks;

public class Mask : BaseDateEntity
{
    [BsonElement("profileId")]
    public ObjectId ProfileId;

    [BsonElement("startTime")]
    public DateTimeOffset StartTime;

    [BsonElement("endTime")]
    public DateTimeOffset EndTime;

    [BsonElement("title")]
    [BsonIgnoreIfNull]
    public string? Title;

    [BsonElement("eventId")]
    [BsonIgnoreIfNull]
    public ObjectId? EventId;

    public Mask(ObjectId profileId, DateTimeOffset startTime, DateTimeOffset endTime, string? title = null)
    {
        ProfileId = profileId;
        StartTime = startTime.ToUniversalTime();
        EndTime = endTime.ToUniversalTime();
        Title = title;
    }

    public Mask(ObjectId profileId, Event ev, string? title = null)
    {
        ProfileId = profileId;
        EventId = ev.Id;
        StartTime = ev.StartTime.ToUniversalTime();
        EndTime = ev.EndTime.ToUniversalTime();
        Title = title;
    }

}

