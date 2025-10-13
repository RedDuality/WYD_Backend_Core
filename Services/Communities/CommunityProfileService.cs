using Core.Components.Database;
using Core.Model.Communities;
using Core.Model.Profiles;
using Core.Services.Notifications;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Communities;

public class CommunityProfileService(MongoDbService dbService) : IProfileFinder
{
    private readonly CollectionName communityProfileCollection = CollectionName.CommunityProfiles;
    public async Task CreateAsync(List<ProfileCommunity> profileCommunities, IClientSessionHandle session)
    {
        List<CommunityProfile> communityProfiles = [];
        foreach (var pc in profileCommunities)
        {
            CommunityProfile cp = new(pc);
            communityProfiles.Add(cp);
        }
        await dbService.CreateManyAsync(communityProfileCollection, communityProfiles, session);
    }

    private async Task<List<CommunityProfile>> FindAllByCommunityId(ObjectId communityId)
    {
        var result = await dbService.RetrieveMultipleAsync(
            communityProfileCollection,
            Builders<CommunityProfile>.Filter.Eq(cp => cp.CommunityId, communityId));

        var eventProfiles = result?.ToList();
        if (eventProfiles == null || eventProfiles.Count == 0)
        {
            throw new InvalidOperationException($"No community profiles found for CommunityId: {communityId}");
        }

        return eventProfiles;
    }

    public async Task<List<ObjectId>> GetProfileIdsAsync(ObjectId communityId)
    {
        var eps = await FindAllByCommunityId(communityId);
        return eps.Select(ep => ep.ProfileId).ToList();
    }
}