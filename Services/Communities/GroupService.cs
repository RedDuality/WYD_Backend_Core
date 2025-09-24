
using Core.Components.Database;
using Core.Model.Communities;
using Core.Model.Profiles;
using MongoDB.Driver;

namespace Core.Services.Communities;

public class GroupService(MongoDbService dbService)
{
    private readonly CollectionName groupCollection = CollectionName.Groups;

    public async Task<Group> CreateAsync(
        HashSet<Profile> profiles,
        Profile owner,
        Community community,
        string? name = null,
        bool? mainGroup = null,
        IClientSessionHandle? session = null)
    {
        var groupProfiles = profiles.Select((p) =>
            {
                return new GroupProfile(p, p.Id == owner.Id ? GroupRole.Owner : GroupRole.Viewer);
            }).ToHashSet();

        var group = new Group(community, name ?? "General", groupProfiles, mainGroup);
        return await dbService.CreateOneAsync(groupCollection, group, session);
    }
}