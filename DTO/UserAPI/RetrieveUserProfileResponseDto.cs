using Core.Model.Profiles;
using Core.Model.Users;

namespace Core.DTO.UserAPI;

public class RetrieveUserProfileResponseDto(Profile profile, UserProfile? userProfile = null)
{
    public string Id { get; set; } = profile.Id.ToString();
    public string Tag { get; set; } = profile.Tag;
    public string Name { get; set; } = profile.Name;
    public DateTimeOffset UpdatedAt { get; set; } = profile.UpdatedAt;
    public long? Color { get; set; } = userProfile?.Color;
    public UserRole? Role { get; set; } = userProfile?.Role;
    public bool? MainProfile { get; set; } = userProfile?.MainProfile;

}