using Core.DTO.CommunityAPI;
using Core.Model.Util.Extensions;

namespace Core.DTO.EventAPI;

public record CreateEventRequestDto {
  required public string Title { get; set; }
  public string? Description { get; set; }
  required public DateTimeOffset StartTime { get; set => field = value.TruncateToSeconds(); }
  required public DateTimeOffset EndTime { get; set => field = value.TruncateToSeconds(); }

  public bool IsAllDay { get; set; } = false;

  public ShareEventRequestDto? ShareDto { get; set; }

  // Parameterless constructor for deserialization
  public CreateEventRequestDto() { }

}
