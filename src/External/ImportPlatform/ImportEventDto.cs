
namespace Core.External.ImportPlatform;

public sealed record ImportEventDto(
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string ImportedAccountUid,
    string ExternalEventId
)
{
    public string? Description { get; init; }
    public bool IsAllDay { get; init; } = false;

    // Recurrence / master event
    public string? ExternalMasterEventId { get; init; }
}


