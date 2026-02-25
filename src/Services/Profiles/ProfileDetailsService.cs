using Core.Components.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using Core.Model.Users;
using Core.Model.Profiles;

namespace Core.Services.Profiles;

public class ProfileDetailsService(MongoDbService dbService)
{
    private readonly CollectionName profileDetailsCollection = CollectionName.ProfileDetails;

    public async Task<ProfileDetails> CreateAsync(Profile profile, IClientSessionHandle session)
    {
        var profileDetails = new ProfileDetails(profile);
        await dbService.CreateOneAsync(profileDetailsCollection, profileDetails, session);

        return profileDetails;
    }

    public async Task<ProfileDetails> AddUser(ObjectId profileId, User user, IClientSessionHandle session)
    {
        var profileUser = new ProfileUser(user);

        var detailsFilter = Builders<ProfileDetails>.Filter.Eq(d => d.ProfileId, profileId);
        var updateDefinition = Builders<ProfileDetails>.Update.AddToSet(pd => pd.Users, profileUser);

        return await dbService.FindOneAndUpdateAsync(profileDetailsCollection, detailsFilter, updateDefinition, session);
    }

    public async Task<ProfileDetails> RetrieveByProfileId(ObjectId profileId)
    {
        return await dbService.RetrieveAsync(
                    profileDetailsCollection,
                    Builders<ProfileDetails>.Filter.Where(p => p.ProfileId == profileId)
                );
    }
    public async Task<List<ProfileDetails>> RetrieveByProfileIds(HashSet<ObjectId> profileIds)
    {
        return await dbService.RetrieveMultipleAsync(
                     profileDetailsCollection,
                     Builders<ProfileDetails>.Filter.In(p => p.ProfileId, profileIds)
                 );

    }


    public async Task<List<User>> RetrieveUsersByProfileIds(HashSet<ObjectId> profileIds)
    {
        var profileDetails = await RetrieveByProfileIds(profileIds);

        var userIds = profileDetails.SelectMany(pd => pd.Users).Select(pu => pu.UserId).ToHashSet();

        return await dbService.RetrieveMultipleAsync(
            CollectionName.Users,
            Builders<User>.Filter.In(u => u.Id, userIds)
        );
    }


}
