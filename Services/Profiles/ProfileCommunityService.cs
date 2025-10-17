using Core.Components.Database;
using Core.Model.Communities;
using Core.Model.Profiles;
using Core.Services.Communities;
using MongoDB.Driver;

namespace Core.Services.Profiles;

public class ProfileCommunityService(MongoDbService dbService, CommunityProfileService communityProfileService)
{
    private readonly CollectionName profileCommunityCollection = CollectionName.ProfileCommunities;
    public async Task<List<ProfileCommunity>> CreateAsync(
        Community community,
        Group group,
        Profile owner,
        HashSet<Profile> profiles,
        IClientSessionHandle session)
    {
        List<ProfileCommunity> profileCommunities = [];
        foreach (var p in profiles)
        {
            Profile? otherProfile = null;
            if (community.Type == CommunityType.Personal)
            {
                if (profiles.Count != 2)
                    throw new ArgumentException("Personal community must be between two profiles");
                otherProfile = profiles.Where((prof) => prof.Id != p.Id).First();
            }

            var profileGroup = new ProfileGroup(
                group,
                p == owner ? GroupRole.Owner : GroupRole.Viewer);

            profileCommunities.Add(
                new ProfileCommunity(p, community, [profileGroup], otherProfile)
            );
        }
        await dbService.CreateManyAsync(profileCommunityCollection, profileCommunities, session);
        await communityProfileService.CreateAsync(profileCommunities, session);

        return profileCommunities;
    }

    public async Task<List<ProfileCommunity>> RetrieveProfileCommunitiesByProfile(Profile profile)
    {
        var filter = Builders<ProfileCommunity>.Filter.Eq((pc) => pc.ProfileId, profile.Id);
        return await dbService.RetrieveMultipleAsync(profileCommunityCollection, filter);
    }

    public async Task<ProfileCommunity?> FindPersonalCommunity(Profile owner, Profile secondaryProfile)
    {
        var filter = Builders<ProfileCommunity>.Filter.Where((pc) => pc.ProfileId == owner.Id && pc.OtherProfileId == secondaryProfile.Id);
        var result = await dbService.RetrieveOrNullAsync(profileCommunityCollection, filter);
        return result;
    }
}