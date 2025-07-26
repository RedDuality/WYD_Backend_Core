/*
namespace Core.Model.Dto;

public class GroupDto
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? Hash { get; set; }
    public string? BlobHash { get; set; }
    public long Color { get; set; } = 4278190080; //black
    public bool? Trusted { get; set; } = false;
    public bool? GeneralForCommunity { get; set; }
    public HashSet<Profile> Profiles { get; set; } = [];


    public GroupDto(Group group, Profile currentProfile
)
    {
        Id = group.Id;
        Name = group.Name;
        Hash = group.Hash;
        BlobHash = group.BlobHash;
        GeneralForCommunity = group.GeneralForCommunity;
        Profiles = group.ProfileGroups.Select((pg) => pg.Profile).ToHashSet();

        var userGroup = group.ProfileGroups.FirstOrDefault(pg => pg.Profile.Id == currentProfile.Id);
        if (userGroup != null)
        {
            Color = userGroup.Color;
            Trusted = userGroup.Trusted;
        }
    }

    public GroupDto()
    {
    }
}
*/