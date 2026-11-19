using Core.Model.Base;
using Core.Model.Util.Extensions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events;

public abstract class BaseEvent : BaseDateEntity
{

  public BaseEvent(String title, DateTimeOffset startTime) : base()
  {
    Title = title;
    StartTime = startTime;
  }

  [BsonElement("title")]
  public string Title { get; set; }

  private DateTimeOffset _startTime;
  [BsonElement("startTime")]
  public DateTimeOffset StartTime { get => _startTime; set => _startTime = value.TruncateToSeconds(); }

  [BsonElement("isAllDay")]
  [BsonIgnoreIfDefault]
  public bool IsAllDay { get; set; } = false;

  abstract public DateTimeOffset EndTime { get; set; }

  abstract public TimeSpan Duration { get; set; }
}
