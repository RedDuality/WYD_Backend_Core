using Core.Model.Events;
using Core.Model.Profiles;

namespace Core.DTO.EventAPI;

public record RetrieveEventResponseDto(Event Ev)
{
    public string Id { get; set; } = GetId(Ev);
    public string Title { get; set; } = Ev.Title;
    public DateTimeOffset StartTime { get; set; } = Ev.StartTime;
    public DateTimeOffset EndTime { get; set; } = Ev.EndTime;
    public DateTimeOffset UpdatedAt { get; set; } = Ev.UpdatedAt;
    public int? TotalProfiles { get; set; } = Ev.TotalProfilesMinusOne != 0 ? Ev.TotalProfilesMinusOne + 1 : null;
    public int? TotalConfirmed { get; set; } = Ev.TotalConfirmedMinusOne != 0 ? Ev.TotalConfirmedMinusOne + 1 : null;

    // --- recurrency
    public string? MasterEventId { get; set; } = Ev.MasterEventId.ToString();
    public string? RecurrencyInstanceId { get; set; } = Ev.RecurrencyInstanceId;
    public bool DetachedInstance {get; set; } = Ev.DetachedInstance;

    // --- import
    public string? ImportedAccountUid { get; set; } = Ev.ImportedAccountUid;


    public List<ProfileEventDto>? ProfileEvents { get; set; }

    public EventDetailsDto? EventDetails { get; set; }


    private static string GetId(Event ev)
    {
        // for generatedRecurrencyInstance have the sameId
        return (ev.MasterEventId == null && ev.DetachedInstance == false) ? ev.Id.ToString() : ev.MasterEventId.ToString() + '_' + ev.RecurrencyInstanceId;  
    }

    // details might be null for update response
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
