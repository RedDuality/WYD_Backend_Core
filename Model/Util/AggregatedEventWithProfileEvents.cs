using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using Core.Model.Join;

namespace Core.Model.Util;

/// <summary>
/// Represents the intermediate result after grouping events.
/// </summary>
public class AggregatedEventWithProfileEvents
{
    // The BsonId attribute is used to map the group key (_id) to this property.
    [BsonId]
    [BsonElement("_id")]
    public required Event Event { get; set; }

    [BsonElement("profileEvents")]
    public required List<ProfileEvent> ProfileEvents { get; set; } = [];
}
