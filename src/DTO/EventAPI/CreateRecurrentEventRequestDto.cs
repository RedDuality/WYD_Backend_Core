namespace Core.DTO.EventAPI;

public class CreateRecurrentEventRequestDto
{
    required public string Title { get; set; }
    public string? Description { get; set; }
    required public DateTimeOffset StartTime { get; set; }
    required public DateTimeOffset EndTime { get; set; }

    public bool IsAllDay { get; set; } = false;

    public required string RecurrenceRule { get; set; }
    public required TimeZoneInfo TimeZone { get; set; }

    public required DateTimeOffset CacheIntervalStart { get; set; }
    public required DateTimeOffset CacheIntervalEnd { get; set; }

    // Parameterless constructor for deserialization
    public CreateRecurrentEventRequestDto() { }

}
