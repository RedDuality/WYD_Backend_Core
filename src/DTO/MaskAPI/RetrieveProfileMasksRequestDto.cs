
using Core.Model.Util.Extensions;

namespace Core.DTO.MaskAPI;

public class RetrieveProfileMaskRequestDto
{
    public required string ProfileId { get; set; }
    public required DateTimeOffset StartTime { get; set => field = value.TruncateToSeconds(); }
    public required DateTimeOffset EndTime { get; set => field = value.TruncateToSeconds(); }

    public RetrieveProfileMaskRequestDto() { }
}

