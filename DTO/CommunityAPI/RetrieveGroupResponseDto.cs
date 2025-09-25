using Core.Model.Communities;
using Core.Model.Profiles;

namespace Core.DTO.CommunityAPI;

public class RetrieveGroupResponseDto(ProfileGroup group)
{
    public string GroupId { get; set; } = group.GroupId.ToString();

    public string Name { get; set; } = group.Name;

    public bool IsMainGroup { get; set; } = group.IsMainGroup;

    public GroupRole Role { get; set; } = group.Role;
}
