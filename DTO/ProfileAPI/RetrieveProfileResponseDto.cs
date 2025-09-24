using Core.Model.Profiles;

namespace Core.DTO.ProfileAPI;

public class RetrieveProfileResponseDto(Profile p)
{
    public string Id { get; set; } = p.Id.ToString();
    public string Tag { get; set; } = p.Tag;
    public string Name { get; set; } = p.Name;
    public DateTimeOffset UpdatedAt { get; set; } = p.UpdatedAt;


}