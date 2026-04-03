namespace Core.DTO.EventAPI;

public record RetrieveMultipleEventsRequestDto
{
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    
    public RetrieveMultipleEventsRequestDto() { }
}