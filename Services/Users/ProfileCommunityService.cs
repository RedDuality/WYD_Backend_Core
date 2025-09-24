using System.Collections.ObjectModel;
using Core.Components.Database;
using Core.Model.Communities;
using Core.Model.Profiles;
using Core.Services.Communities;

namespace Core.Services.Users;

public class ProfileCommunityService(MongoDbService dbService, CommunityProfileService communityProfileService)
{
    private readonly CollectionName profileCommunityCollection = CollectionName.ProfileCommunities;
    public async Task<List<ProfileCommunity>> CreateAsync(Community community, Group group, Profile owner, HashSet<Profile> profiles)
    {
        List<ProfileCommunity> profileCommunities = [];
        foreach (var p in profiles)
        {
            var profileGroup = new ProfileGroup(group, p == owner ? GroupRole.Owner : GroupRole.Viewer);

            profileCommunities.Add(
                new ProfileCommunity(p, community, [profileGroup])
            );
        }
        await dbService.CreateManyAsync(profileCommunityCollection, profileCommunities);
        await communityProfileService.CreateAsync(profileCommunities);

        return profileCommunities;
    }
}