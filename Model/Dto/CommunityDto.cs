/*
using Model;

namespace Core.Model.Dto;

public class CommunityDto(Community community, Profile profile)
{
    public int Id {get; set; } = community.Id;
    public string Name { get; set; } = community.Name;
    public string? BlobHash { get; set; } = community.BlobHash;
    public CommunityType Type { get; set; } = community.Type;

    public ICollection<GroupDto> Groups { get; set; } = community.Groups.Select((g) => new GroupDto(g, profile)).ToList();

    public HashSet<Profile> Profiles {get; set; } = community.Type == CommunityType.Community ? community.Profiles : [];

}*/