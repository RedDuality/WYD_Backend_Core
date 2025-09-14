namespace Core.DTO.EventAPI;

public class CreateEventRequestDto
{
    required public string Title { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }

    // Parameterless constructor for deserialization
    public CreateEventRequestDto() { }

}
