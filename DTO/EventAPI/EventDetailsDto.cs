using Core.Model.Events;

namespace Core.DTO.EventAPI;

public class EventDetailsDto(EventDetails ed)
{
    public string Hash { get; set; } = ed.Id.ToString();
    public string? Description { get; set; } = ed.Description;
    public long TotalImages { get; set; } = ed.TotalImages;
    public DateTimeOffset UpdatedAt { get; set; } = ed.UpdatedAt;

}