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


}
