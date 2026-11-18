using Core.Model.Events;
using Core.Model.Events.Recurrence;
using Core.Model.Profiles;

namespace Core.DTO.EventAPI;

public class RetrieveRecurrentEventResponseDto(RecurrentEvent Ev) {
  public string Id { get; set; } = Ev.Id.ToString();
  public string Title { get; set; } = Ev.Title;
  public DateTimeOffset StartTime { get; set; } = Ev.StartTime;
  public DateTimeOffset EndTime { get; set; } = Ev.EndTime;
  public DateTimeOffset UpdatedAt { get; set; } = Ev.UpdatedAt;

  // --- recurrency
  public DateTimeOffset? RecurrenceEnd { get; set; } = Ev.RecurrenceEnd;

  // TODO TimeZone
  public string RecurrenceRule { get; set; } = Ev.RecurrenceRule;


  // --- import
  public string? ImportedAccountUid { get; set; } = Ev.ImportedAccountUid;


  public List<RetrieveProfileEventResponseDto>? ProfileEvents { get; set; }

  public RetrieveEventDetailsResponseDto? EventDetails { get; set; }

  public RetrieveRecurrentEventResponseDto(RecurrentEvent ev, EventDetails? details = null)
      : this(ev, details, profileEventDtos: null) { }

  public RetrieveRecurrentEventResponseDto(RecurrentEvent ev, EventDetails? details = null, IEnumerable<ProfileEvent>? profileEvents = null)
      : this(ev, details, profileEvents?.Select(pe => new RetrieveProfileEventResponseDto(pe)).ToList()) { }

  public RetrieveRecurrentEventResponseDto(RecurrentEvent ev, EventDetails? details = null, List<RetrieveProfileEventResponseDto>? profileEventDtos = null) : this(ev) {
    if (details != null)
      EventDetails = new RetrieveEventDetailsResponseDto(details);
    if (profileEventDtos != null)
      ProfileEvents = profileEventDtos;

  }

}
