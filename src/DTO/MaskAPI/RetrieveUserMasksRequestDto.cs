
namespace Core.DTO.MaskAPI;

public class RetrieveUserMaskRequestDto
{
    public required DateTimeOffset StartTime { get; set; }
    public required DateTimeOffset? EndTime { get; set; }

    public RetrieveUserMaskRequestDto() { }
}

