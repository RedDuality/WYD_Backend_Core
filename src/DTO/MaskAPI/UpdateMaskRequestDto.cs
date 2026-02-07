namespace Core.DTO.MaskAPI;

public class UpdateMaskRequestDto
{
    required public string MaskId { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }

    public UpdateMaskRequestDto() { }

}


