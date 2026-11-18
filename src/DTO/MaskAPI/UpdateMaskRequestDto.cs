using Core.Model.Util.Extensions;

namespace Core.DTO.MaskAPI;

public class UpdateMaskRequestDto {
  required public string MaskId { get; set; }
  public string? Title { get; set; }
  public DateTimeOffset? StartTime { get; set => field = value?.TruncateToSeconds(); }
  public DateTimeOffset? EndTime { get; set => field = value?.TruncateToSeconds(); }

  public UpdateMaskRequestDto() { }
}