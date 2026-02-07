
namespace Core.DTO.MaskAPI;

public class RetrieveUserMaskRequestDto
{
    public required List<string> ProfileIds { get; set; }
    public required DateTimeOffset StartTime { get; set; }
    public required DateTimeOffset EndTime { get; set; }

    public RetrieveUserMaskRequestDto() { }
}

