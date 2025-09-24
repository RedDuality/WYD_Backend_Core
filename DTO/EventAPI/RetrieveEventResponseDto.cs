using Core.Model;
using Core.Model.Events;
using Core.Model.Profiles;

namespace Core.DTO.EventAPI;

public class RetrieveEventResponseDto(Event ev)
{
    public string Hash { get; set; } = ev.Id.ToString();
    public string Title { get; set; } = ev.Title;
    public DateTimeOffset StartTime { get; set; } = ev.StartTime;
    public DateTimeOffset EndTime { get; set; } = ev.EndTime;
    public DateTimeOffset UpdatedAt { get; set; } = ev.UpdatedAt;
    public int? TotalProfiles { get; set; } = ev.TotalProfilesMinusOne != 0 ? ev.TotalProfilesMinusOne + 1 : null;
    public int? TotalConfirmed { get; set; } = ev.TotalConfirmedMinusOne != 0 ? ev.TotalConfirmedMinusOne + 1 : null;


    public List<ProfileEventDto>? ProfileEvents { get; set; }

    public EventDetailsDto? EventDetails { get; set; }



    public RetrieveEventResponseDto(Event ev, EventDetails? details = null)
        : this(ev, details, profileEventDtos: null)
    { }

    public RetrieveEventResponseDto(Event ev, EventDetails? details = null, IEnumerable<ProfileEvent>? profileEvents = null)
        : this(ev, details, profileEvents?.Select(pe => new ProfileEventDto(pe)).ToList())
    { }

    public RetrieveEventResponseDto(Event ev, EventDetails? details = null, List<ProfileEventDto>? profileEventDtos = null) : this(ev)
    {
        if (details != null)
            EventDetails = new EventDetailsDto(details);
        if (profileEventDtos != null)
            ProfileEvents = profileEventDtos;
    }

}
