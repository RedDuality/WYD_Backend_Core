namespace Core.DTO.EventAPI;

public class UpdateEventRequestDto
{
    public required string EventId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }

    // Parameterless constructor for deserialization
    public UpdateEventRequestDto() { }

}
