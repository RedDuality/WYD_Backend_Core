using Core.Model.Util.Extensions;

namespace Core.DTO.EventAPI;

public class CreateRecurrentEventRequestDto {
  required public string Title { get; set; }
  public string? Description { get; set; }
  required public DateTimeOffset StartTime { get; set => field = value.TruncateToSeconds(); }
  required public DateTimeOffset EndTime { get; set => field = value.TruncateToSeconds(); }

  public bool IsAllDay { get; set; } = false;

  public required string RecurrenceRule { get; set; }
  public required string TimeZone { get; set; }

  public required DateTimeOffset CacheIntervalStart { get; set; }
  public required DateTimeOffset CacheIntervalEnd { get; set; }

  // Parameterless constructor for deserialization
  public CreateRecurrentEventRequestDto() { }

  public TimeZoneInfo GetTimeZoneInfo() {
    try {
      return TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
    }
    catch {
      throw new ArgumentException("Selected timezone is not valid");
    }
  }

}
