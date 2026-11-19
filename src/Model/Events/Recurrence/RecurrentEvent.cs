using Core.Model.Util.Extensions;
using Core.Services.Util;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.Model.Events.Recurrence;

public class RecurrentEvent : BaseEvent
{

	public override DateTimeOffset EndTime
	{
		get => (StartTime + Duration).TruncateToSeconds();
		set
		{
			Duration = value - StartTime;
		}
	}

	private TimeSpan _duration;
	[BsonElement("duration")]
	public override TimeSpan Duration
	{
		get => _duration;
		set
		{
			var floored = TimeSpan.FromSeconds(Math.Floor(value.TotalSeconds));

			if (floored < TimeSpan.FromMinutes(5))
				throw new ArgumentException("Event must be at least 5 minutes long", nameof(value));

			_duration = floored;
		}
	}


	// --- Recurrence ---

	[BsonElement("recurrenceEnd")]
	public DateTimeOffset RecurrenceEnd { get; set; }

	[BsonElement("timeZone")]
	[BsonIgnoreIfNull]
	public TimeZoneInfo TimeZone { get; set; }

	[BsonElement("recurrenceRule")]
	public string RecurrenceRule { get; set; }

	// --- imported values ---

	[BsonElement("accountUid")]
	[BsonIgnoreIfNull]
	public string? ImportedAccountUid { get; set; }

	[BsonElement("importedId")]
	[BsonIgnoreIfNull]
	public string? ExternalEventId { get; set; }

	public RecurrentEvent(string title,
		DateTimeOffset startTime,
		DateTimeOffset endTime,
		TimeZoneInfo timeZone,
		string recurrenceRule
	) : base(title, startTime)
	{
		var validRule = RecurrenceService.GetValidRule(recurrenceRule);

		var recurrenceEnd = RecurrenceService.ExtractRecurrenceEnd(validRule, timeZone);

		if (recurrenceEnd < startTime)
			throw new ArgumentException(
					$"Recurrence UNTIL ({recurrenceEnd}) cannot be earlier than StartTime ({startTime}).",
					nameof(recurrenceRule)
			);

		TimeZone = timeZone;
		RecurrenceRule = validRule;
		RecurrenceEnd = recurrenceEnd;
		EndTime = endTime;
	}

	public RecurrentEvent(string title,
	DateTimeOffset startTime,
	TimeSpan duration,
	TimeZoneInfo timeZone,
	string recurrenceRule
) : base(title, startTime)
	{
		var validRule = RecurrenceService.GetValidRule(recurrenceRule);

		var recurrenceEnd = RecurrenceService.ExtractRecurrenceEnd(validRule, timeZone);

		if (recurrenceEnd < startTime)
			throw new ArgumentException(
					$"Recurrence UNTIL ({recurrenceEnd}) cannot be earlier than StartTime ({startTime}).",
					nameof(recurrenceRule)
			);

		TimeZone = timeZone;
		RecurrenceRule = validRule;
		RecurrenceEnd = recurrenceEnd;
		Duration = duration;
	}
}