using Core.Model.Communities;
using Core.Model.Profiles;

namespace Core.DTO.CommunityAPI;

public class RetrieveCommunityResponseDto(ProfileCommunity profileCommunity)
{
    public string Id { get; set; } = profileCommunity.CommunityId.ToString();

    public string Name { get; set; } = profileCommunity.Name;

    public CommunityType Type { get; set; } = profileCommunity.Type;

    public DateTimeOffset updatedAt = profileCommunity.CommunityUpdatedAt;

    public HashSet<ProfileGroup> Groups = profileCommunity.Groups;

}