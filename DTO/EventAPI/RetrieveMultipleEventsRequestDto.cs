namespace Core.DTO.EventAPI;

public class RetrieveEventsRequestDto
{
    public List<string> ProfileHashes { get; set; } = [];
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }

    public RetrieveEventsRequestDto() { }
}