namespace Core.DTO.MediaAPI;

public class MediaReadRequestDto

{
    public string ParentHash { get; set; } = "";

    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }


    public MediaReadRequestDto() { }

}