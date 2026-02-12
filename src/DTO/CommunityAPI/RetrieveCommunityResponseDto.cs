using Core.Model.Communities;
using Core.Model.Profiles;

namespace Core.DTO.CommunityAPI;

public class RetrieveCommunityResponseDto(ProfileCommunity profileCommunity)
{
    public string Id { get; set; } = profileCommunity.CommunityId.ToString();

    public string? Name { get; set; } = profileCommunity.Name;

    public string? OtherProfileId { get; set; } = profileCommunity.OtherProfileId.ToString();
    
    public CommunityType Type { get; set; } = profileCommunity.Type;

    public DateTimeOffset UpdatedAt { get; set; } = profileCommunity.CommunityUpdatedAt;

    public HashSet<RetrieveGroupResponseDto> Groups { get; set; } = profileCommunity.Groups.Select((g) => new RetrieveGroupResponseDto(g)).ToHashSet();

}