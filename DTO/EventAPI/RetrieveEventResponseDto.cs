using System.Text.Json.Serialization;
using Core.DTO.Model;
using Core.Model;
using Core.Model.Details;
using Core.Model.Join;

namespace Core.DTO.EventAPI;

public class RetrieveEventResponseDto(Event ev)
{
    public string Hash { get; set; } = ev.Id.ToString();
    public string Title { get; set; } = ev.Title;
    public DateTimeOffset StartTime { get; set; } = ev.StartTime;
    public DateTimeOffset EndTime { get; set; } = ev.EndTime;
    public int? TotalConfirmed { get; set; }
    public int? TotalProfiles { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProfileEventDto>? ProfileEvents { get; set; }


    public EventDetailsDto? EventDetails { get; set; }

    public RetrieveEventResponseDto(Event ev, EventDetails eventDetails, List<ProfileEvent> profileEvents)
        : this(ev, eventDetails, profileEvents.Select((pe) => new ProfileEventDto(pe)).ToList())
    {

    }

    public RetrieveEventResponseDto(Event ev, EventDetails eventDetails, List<ProfileEventDto> profileEvents) : this(ev, profileEvents)
    {
        EventDetails = new EventDetailsDto(eventDetails);
    }

    public RetrieveEventResponseDto(Event ev, List<ProfileEventDto> profileEvents) : this(ev)
    {
        ProfileEvents = profileEvents;
    }

    public RetrieveEventResponseDto(Event ev, EventDetails eventDetails) : this(ev)
    {
        EventDetails = new EventDetailsDto(eventDetails);
    }
}
