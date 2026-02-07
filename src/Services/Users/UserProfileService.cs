using Core.Components.Database;
using MongoDB.Driver;
using Core.Model.Users;
using Core.Model.Profiles;
using MongoDB.Bson;

namespace Core.Services.Users;


public class UserProfileService(MongoDbService dbService)
{

    private readonly CollectionName userProfilesCollection = CollectionName.UserProfiles;

    public async Task<UserProfile> Create(User user, Profile profile, IClientSessionHandle session)
    {
        var userProfile = new UserProfile(user, profile);
        // TODO add viewSettings over other profiles
        await dbService.CreateOneAsync(userProfilesCollection, userProfile, session);

        return userProfile;
    }

    public async Task<List<UserProfile>> RetrieveFromUser(ObjectId userId)
    {
        var filter = Builders<UserProfile>.Filter.Eq(up => up.UserId, userId);

        return await dbService.RetrieveMultipleAsync(userProfilesCollection, filter);
    }

    public async Task<UserProfile?> RetrieveFromUserAndProfile(ObjectId userId, ObjectId profileId)
    {
        var filter = Builders<UserProfile>.Filter.And(
            Builders<UserProfile>.Filter.Eq(uc => uc.UserId, userId),
            Builders<UserProfile>.Filter.Eq(uc => uc.ProfileId, profileId)
        );

        return await dbService.RetrieveOrNullAsync(userProfilesCollection, filter);
    }

    public async Task<UserProfile?> Update(ObjectId userId, ObjectId profileId, long color, IClientSessionHandle session)
    {
        var filter = Builders<UserProfile>.Filter.And(
            Builders<UserProfile>.Filter.Eq(uc => uc.UserId, userId),
            Builders<UserProfile>.Filter.Eq(uc => uc.ProfileId, profileId)
        );
        
        var updateDefinition = Builders<UserProfile>.Update.Set(up => up.Color, color);

        return await dbService.FindOneAndUpdateAsync(userProfilesCollection, filter, updateDefinition, session: session);
    }

}