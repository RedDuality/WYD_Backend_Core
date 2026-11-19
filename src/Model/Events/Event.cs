using Core.Model.Util.Extensions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events;

public class Event : BaseEvent {
  public Event(string title, DateTimeOffset startTime, DateTimeOffset endTime) : base(title, startTime) {
    EndTime = endTime;
  }

  private DateTimeOffset _endTime;

  [BsonElement("endTime")]
  public override DateTimeOffset EndTime {
    get => _endTime;
    set {
      if (value < StartTime.AddMinutes(5))
        throw new ArgumentException("Event must be at least 5 minutes long", nameof(value));

      _endTime = value.TruncateToSeconds();
    }
  }

  public override TimeSpan Duration {
    get => EndTime - StartTime; 
    set {
      EndTime = StartTime.Add(value);
    }
  }

  [BsonElement("TotalProfiles")]
  [BsonIgnoreIfDefault]
  public int TotalProfilesMinusOne { get; set; } = 0;

  [BsonElement("TotalConfirmed")]
  [BsonIgnoreIfDefault]
  public int TotalConfirmedMinusOne { get; set; } = 0;

  // --- Recurrency: Detached Instance ---

  [BsonElement("masterEventId")]
  [BsonIgnoreIfNull]
  public ObjectId? MasterEventId { get; set; }

  [BsonElement("recurrencyId")]
  [BsonIgnoreIfNull]
  public string? RecurrencyInstanceId { get; set; } //MasterId_yyyyMMddTHHmmssZ (all-day ones have MasterId_yyyyMMdd)

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