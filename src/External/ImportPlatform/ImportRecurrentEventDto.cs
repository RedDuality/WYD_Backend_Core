
namespace Core.External.ImportPlatform;

public sealed record ImportRecurrentEventDto(
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    TimeZoneInfo TimeZone,
    string ImportedAccountUid,
    string RecurrenceRule,
    string ExternalEventId
)
{
    public string? Description { get; init; }
    public bool IsAllDay { get; init; }

    public DateTimeOffset? RecurrenceEnd;
}


