
using Core.Model.Util.Extensions;

namespace Core.DTO.MaskAPI;

public class RetrieveUserMaskRequestDto
{
    public required DateTimeOffset StartTime { get; set => field = value.TruncateToSeconds(); }
    public required DateTimeOffset? EndTime { get; set => field = value?.TruncateToSeconds(); }

    public RetrieveUserMaskRequestDto() { }
}

