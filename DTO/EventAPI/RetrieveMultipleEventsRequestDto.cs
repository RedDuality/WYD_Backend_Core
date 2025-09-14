namespace Core.DTO.EventAPI;

public class RetrieveMultipleEventsRequestDto
{
    public List<string> ProfileHashes { get; set; } = [];
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }

    public RetrieveMultipleEventsRequestDto() { }
}