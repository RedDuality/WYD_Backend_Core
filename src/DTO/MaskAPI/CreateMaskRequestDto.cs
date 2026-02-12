namespace Core.DTO.MaskAPI;

public class CreateMaskRequestDto
{
    public string? Title { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public CreateMaskRequestDto() { }

}


