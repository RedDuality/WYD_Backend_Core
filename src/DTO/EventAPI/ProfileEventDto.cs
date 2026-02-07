using Core.Model.Profiles;

namespace Core.DTO.EventAPI;

public class ProfileEventDto
{
    public string? ProfileId { get; set; }
    public EventRole Role { get; set; }
    public bool Confirmed { get; set; }
    public bool Trusted { get; set; }

    // Parameterized constructor for custom initialization
    public ProfileEventDto(ProfileEvent pe)
    {
        ProfileId = pe.ProfileId.ToString();
        Role = pe.Role;
        Confirmed = pe.Confirmed;
        //TODO
        Trusted = false;
    }

    // Parameterless constructor for deserialization
    public ProfileEventDto() { }
}
