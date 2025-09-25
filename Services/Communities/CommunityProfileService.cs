using Core.Components.Database;
using Core.Model.Communities;
using Core.Model.Profiles;
using MongoDB.Driver;

namespace Core.Services.Communities;

public class CommunityProfileService(MongoDbService dbService)
{
    private readonly CollectionName communityProfileCollection = CollectionName.CommunityProfiles;
    public async Task CreateAsync(List<ProfileCommunity> profileCommunities, IClientSessionHandle session)
    {
        List<CommunityProfile> communityProfiles = [];
        foreach (var pc in profileCommunities)
        {
            communityProfiles.Add(
                new CommunityProfile(pc)
            );
        }
        await dbService.CreateManyAsync(communityProfileCollection, communityProfiles, session);
    }
}