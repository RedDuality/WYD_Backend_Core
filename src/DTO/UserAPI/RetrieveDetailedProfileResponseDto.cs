using Core.Model.Profiles;
using Core.Model.Users;

namespace Core.DTO.UserAPI;

// TODO use only RetrieveUserProfileResponseDto

public class RetrieveDetailedProfileResponseDto(Profile profile, UserProfile? up, UserClaims? uc)
{
    public string Id { get; set; } = profile.Id.ToString();
    public string Tag { get; set; } = profile.Tag;
    public string Name { get; set; } = profile.Name;
    public DateTimeOffset UpdatedAt { get; set; } = profile.UpdatedAt;
    public long? Color { get; set; } = up?.Color;

    public List<ViewSettingsDto>? ViewSettings { get; set; } = up?.ViewSettings?
            .Select(vs => new ViewSettingsDto(vs))
            .ToList();

    public HashSet<string>? UserClaims { get; set; } = uc?.Claims?
            .Select(c => c.Claim.ToString())
            .ToHashSet();
}
