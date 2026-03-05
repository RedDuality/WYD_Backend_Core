namespace Core.DTO.EventAPI;

public class RetrieveUpdatedEventsRequestDto
{
    public List<string> ProfileIds { get; set; } = [];
    public DateTimeOffset UpdatedAfterTime { get; set; }

    public RetrieveUpdatedEventsRequestDto() { }
}