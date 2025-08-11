using Core.Model;
using Core.Model.Details;
using Core.Model.Join;
using Core.Services.Database;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Model;

public class ProfileDetailsService(MongoDbService dbService)
{
    private readonly CollectionName profileDetailsCollection = CollectionName.ProfileDetails;

    public async Task<ProfileDetails> CreateAsync(Profile profile, User user, IClientSessionHandle session)
    {
        var profileDetails = new ProfileDetails(profile);
        await dbService.CreateOneAsync(profileDetailsCollection, profileDetails, session);

        var updateDefinition = Builders<Profile>.Update.Set(p => p.DetailsId, profileDetails.Id);
        await dbService.PatchUpdateAsync(CollectionName.Profiles, profile.Id, updateDefinition, session);

        return profileDetails;
    }

    public async Task<ProfileDetails> AddUser(ObjectId detailsId, User user, IClientSessionHandle session)
    {
        var profileUser = new ProfileUser(user);

        var updateDefinition = Builders<ProfileDetails>.Update.AddToSet(pd => pd.Users, profileUser);
        return await dbService.PatchUpdateAsync(profileDetailsCollection, detailsId, updateDefinition, session);
    }

}
