using Core.DTO.CommunityAPI;

namespace Core.DTO.EventAPI;

public record CreateEventRequestDto
{
    required public string Title { get; set; }
    public string? Description { get; set; }
    required public DateTimeOffset StartTime { get; set; }
    required public DateTimeOffset EndTime { get; set; }

    public bool IsAllDay { get; set; } = false;

    public ShareEventRequestDto? ShareDto { get; set; }

    // Parameterless constructor for deserialization
    public CreateEventRequestDto() { }

}
