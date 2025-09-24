using Core.Components.Database;
using Core.Model.Communities;
using Core.Model.Profiles;

namespace Core.Services.Communities;

public class CommunityProfileService(MongoDbService dbService)
{
    private readonly CollectionName communityProfileCollection = CollectionName.CommunityProfiles;
    public async Task CreateAsync(List<ProfileCommunity> profileCommunities)
    {
        List<CommunityProfile> communityProfiles = [];
        foreach (var pc in profileCommunities)
        {
            communityProfiles.Add(
                new CommunityProfile(pc)
            );
        }
        await dbService.CreateManyAsync(communityProfileCollection, communityProfiles);
    }
}