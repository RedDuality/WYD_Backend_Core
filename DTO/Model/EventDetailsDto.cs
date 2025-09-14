
using Core.Model.Details;



namespace Core.DTO.Model;

public class EventDetailsDto(EventDetails ed)
{

    public string Hash { get; set; } = ed.Id.ToString();

    public string? Description { get; set; }

    public long TotalImages { get; set; } = 0;
    
    
}