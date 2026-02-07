
namespace Core.DTO.MaskAPI;

public class RetrieveProfileMaskRequestDto
{
    public required string ProfileId { get; set; }
    public required DateTimeOffset StartTime { get; set; }
    public required DateTimeOffset EndTime { get; set; }

    public RetrieveProfileMaskRequestDto() { }
}

