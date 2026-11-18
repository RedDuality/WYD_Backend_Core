using Core.Model.Util.Extensions;

namespace Core.DTO.EventAPI;

public class UpdateEventRequestDto {
  public required string EventId { get; set; }
  public string? Title { get; set; }
  public string? Description { get; set; }
  public DateTimeOffset? StartTime { get; set => field = value?.TruncateToSeconds(); }
  public DateTimeOffset? EndTime { get; set => field = value?.TruncateToSeconds(); }


  // Parameterless constructor for deserialization
  public UpdateEventRequestDto() { }

}
