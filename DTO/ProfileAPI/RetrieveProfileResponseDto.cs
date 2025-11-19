using Core.Model.Profiles;
using MongoDB.Bson;

namespace Core.DTO.ProfileAPI;

// TODO use only RetrieveUserProfileResponseDto

public class RetrieveProfileResponseDto(Profile profile)
{
    public string Id { get; set; } = profile.Id.ToString();
    public string? Tag { get; set; } = profile.Tag;
    public string? Name { get; set; } = profile.Name;
    public DateTimeOffset? UpdatedAt { get; set; } = profile.UpdatedAt;
}
