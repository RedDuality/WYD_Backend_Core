using Core.Model.Profiles;
using Core.Model.Users;

namespace Core.DTO.ProfileAPI;

public class UpdateProfileResponseDto(Profile? profile, long? color)
{
    public string? Tag { get; set; } = profile?.Tag;
    public string? Name { get; set; } = profile?.Name;
    public DateTimeOffset? UpdatedAt { get; set; } = profile?.UpdatedAt;
    public long? Color { get; set; } = color;
    //public UserRole? Role { get; set; } = userProfile?.Role;
    //public bool? MainProfile { get; set; } = userProfile?.MainProfile;

}