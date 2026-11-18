using Core.Model.Util.Extensions;

namespace Core.DTO.EventAPI;

public class RetrieveUpdatedEventsRequestDto {
  public List<string> ProfileIds { get; set; } = [];
  public DateTimeOffset UpdatedAfterTime {
    get;
    set => field = value.TruncateToSeconds();
  }

  public RetrieveUpdatedEventsRequestDto() { }
}