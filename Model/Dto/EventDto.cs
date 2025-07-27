namespace Core.Model.Dto;

public class EventDto
{
    public string? Hash { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public int? GroupId { get; set; } = 0;

    public List<string> BlobHashes {get; set;}  = [];
    public List<ProfileEventDto> ProfileEvents { get; set; } = [];

    // Parameterized constructor for custom use
    public EventDto(Event ev)
    {
        Hash = ev.Id.ToString();
        Title = ev.Title;
        Description = ev.Description;
        StartTime = ev.StartTime.ToUniversalTime();
        EndTime = ev.EndTime.ToUniversalTime();
        //GroupId = ev.Group != null ? ev.Group.Id : -1;
        //BlobHashes = ev.Blobs.Select( i => i.Hash).ToList();
        //ProfileEvents = ev.ProfileEvents.Select(pe => new ProfileEventDto(pe)).ToList();
    }

    public EventDto()
    {
        
    }

}
