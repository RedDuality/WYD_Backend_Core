using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events;

public class Event(
    string title,
    DateTimeOffset startTime,
    DateTimeOffset endTime) : BaseEvent(title, startTime, endTime)
{
    [BsonElement("TotalProfiles")]
    [BsonIgnoreIfDefault]
    public int TotalProfilesMinusOne { get; set; } = 0;

    [BsonElement("TotalConfirmed")]
    [BsonIgnoreIfDefault]
    public int TotalConfirmedMinusOne { get; set; } = 0;

    // --- Recurrence ---

    [BsonElement("masterEventId")]
    [BsonIgnoreIfNull]
    public ObjectId? MasterEventId { get; set; }

    [BsonElement("recurrencyId")]
    [BsonIgnoreIfNull]
    public string? RecurrencyInstanceId { get; set; } //yyyyMMddTHHmmssZ;

    [BsonElement("detached")]
    [BsonIgnoreIfDefault]
    public bool DetachedInstance { get; set; } = false;

    // --- Imported values ---

    [BsonElement("accountUid")]
    [BsonIgnoreIfNull]
    public string? ImportedAccountUid { get; set; }

    [BsonElement("importedId")]
    [BsonIgnoreIfNull]
    public string? ExternalEventId { get; set; }

    [BsonElement("exMasterEventId")]
    [BsonIgnoreIfNull]
    public string? ExternalMasterEventId { get; set; }
}

