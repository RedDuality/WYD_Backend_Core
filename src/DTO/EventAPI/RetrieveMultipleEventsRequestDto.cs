namespace Core.DTO.EventAPI;

public record RetrieveMultipleEventsRequestDto
{
    public List<string> ProfileIds { get; set; } = [];
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }

    public RetrieveMultipleEventsRequestDto() { }
}