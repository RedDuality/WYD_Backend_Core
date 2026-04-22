using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events.Recurrence;

public class DetachedInstances(ObjectId masterId, HashSet<DetachedInstance> instances) : BaseEntity
{
    [BsonElement("masterId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public ObjectId MasterId { get; set; } = masterId;

    [BsonElement("instances")]
    public HashSet<DetachedInstance> Instances { get; set; } = instances;

}

public class DetachedInstance(ObjectId eventId, string recurrencyId, DateTimeOffset startTime)
{
    [BsonElement("eventId")]
    public ObjectId EventId { get; set; } = eventId;

    [BsonElement("recurrencyId")]
    public string RecurrencyId { get; set; } = recurrencyId;

    [BsonElement("startTime")]
    public DateTimeOffset StartTime { get; set; } = startTime;

    // RecurrencyId uniquely identifies a slot; two detached instances
    // cannot occupy the same slot in the same series.
    public bool Equals(DetachedInstance? other) =>
        other is not null && RecurrencyId == other.RecurrencyId;

    public override bool Equals(object? obj) => Equals(obj as DetachedInstance);
    public override int GetHashCode() => RecurrencyId.GetHashCode();
}