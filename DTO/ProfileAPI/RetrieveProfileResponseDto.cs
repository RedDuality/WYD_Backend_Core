using Core.Model.Profiles;
using Core.Model.Users;
using MongoDB.Bson;

namespace Core.DTO.ProfileAPI;

// TODO use only RetrieveUserProfileResponseDto

public class RetrieveProfileResponseDto
{
    public string Id { get; set; }
    public string? Tag { get; set; }
    public string? Name { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public long? Color { get; set; }
    public bool? MainProfile { get; set; }

    // Constructor using Profile and optional UserProfile
    public RetrieveProfileResponseDto(Profile profile, UserProfile? userProfile = null)
    {
        Id = profile.Id.ToString();
        Tag = profile.Tag;
        Name = profile.Name;
        UpdatedAt = profile.UpdatedAt;
        Color = userProfile?.Color;
        MainProfile = userProfile?.MainProfile;
    }

    // Constructor using profileId and UserProfile
    public RetrieveProfileResponseDto(ObjectId profileId, UserProfile userProfile)
    {
        Id = profileId.ToString();
        Color = userProfile.Color;
        MainProfile = userProfile.MainProfile;
    }
}
