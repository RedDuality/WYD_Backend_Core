/*
using Core.Model.Enum;

namespace Core.Model.Dto;

public class ProfileEventDto
{
    public string ProfileHash { get; set; }
    public EventRole Role { get; set; }
    public bool Confirmed { get; set; }
    public bool Trusted { get; set; }

    // Parameterized constructor for custom initialization
    public ProfileEventDto(ProfileEvent pe)
    {
        ProfileHash = pe.Profile.Hash;
        Role = pe.Role;
        Confirmed = pe.Confirmed;
        Trusted = pe.Trusted;
    }

    // Parameterless constructor for deserialization
    public ProfileEventDto() { }
}
*/