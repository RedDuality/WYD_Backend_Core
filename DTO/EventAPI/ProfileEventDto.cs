using Core.Model.Profiles;
using Core.Model.Profiles;

namespace Core.DTO.EventAPI;

public class ProfileEventDto
{
    public string? ProfileHash { get; set; }
    public EventRole Role { get; set; }
    public bool Confirmed { get; set; }
    public bool Trusted { get; set; }

    // Parameterized constructor for custom initialization
    public ProfileEventDto(ProfileEvent pe)
    {
        ProfileHash = pe.ProfileId.ToString();
        Role = pe.Role;
        Confirmed = pe.Confirmed;
        //TODO
        Trusted = false;
    }

    // Parameterless constructor for deserialization
    public ProfileEventDto() { }
}
