namespace Core.DTO.CommunityAPI;

public class ShareEventRequestDto
{
    required public List<ShareGroupIdentifierDto> SharedGroups { get; set; }

    public ShareEventRequestDto() { }
}

public class ShareGroupIdentifierDto
{
    required public string CommunityId { get; set; }

    required public string GroupId { get; set; }

    public ShareGroupIdentifierDto() { }
}