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
    required public ObjectId EventId { get; set; } = eventId;

    [BsonElement("recurrencyId")]
    required public string RecurrencyId { get; set; } = recurrencyId;

    [BsonElement("startTime")]
    required public DateTimeOffset StartTime { get; set; } = startTime;
}