using Core.Model;
using Core.Model.Enum;
using Core.Model.Join;

namespace Core.DTO.Model;

public class UserProfileDto(Profile profile, UserProfile userProfile)
{
    public string Hash { get; set; } = profile.Id.ToString();
    public string Tag { get; set; } = profile.Tag;
    public string Name { get; set; } = profile.Name;
    public string? BlobHash { get; set; } = "";
    public long? Color { get; set; } = userProfile.Color;
    public UserRole Role { get; set; } = userProfile.Role;
    public bool MainProfile { get; set; } = userProfile.MainProfile;
}