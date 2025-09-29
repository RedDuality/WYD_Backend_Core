namespace Core.DTO.CommunityAPI;

public class ShareEventRequestDto
{
    required public string CommunityId { get; set; }

    required public string GroupId { get; set; }

    public ShareEventRequestDto() { }
}
