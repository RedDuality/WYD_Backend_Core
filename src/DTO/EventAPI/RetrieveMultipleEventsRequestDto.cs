using Core.Model.Util.Extensions;

namespace Core.DTO.EventAPI;

public record RetrieveMultipleEventsRequestDto {
  required public DateTimeOffset StartTime { get; set => field = value.TruncateToSeconds(); }
  required public DateTimeOffset EndTime { get; set => field = value.TruncateToSeconds(); }
  public RetrieveMultipleEventsRequestDto() { }
}