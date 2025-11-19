using Core.Components.Database;
using MongoDB.Driver;
using Core.Model.Users;
using MongoDB.Bson;
using Core.Model.Profiles;

namespace Core.Services.Users;


public class UserClaimService(MongoDbService dbService)
{

    private readonly CollectionName userClaimCollection = CollectionName.UserClaims;

    public async Task<bool> CheckHasPermit(ObjectId userId, ObjectId profileId, UserClaimType claim)
    {

        // Build filter for userId and profileId
        var filter = Builders<UserClaims>.Filter.And(
            Builders<UserClaims>.Filter.Eq(uc => uc.UserId, userId),
            Builders<UserClaims>.Filter.Eq(uc => uc.ProfileId, profileId),
            Builders<UserClaims>.Filter.ElemMatch(
                uc => uc.Claims,
                c => c.Claim == claim
            )
        );

        // Try to retrieve the document
        var result = await dbService.RetrieveOrNullAsync(userClaimCollection, filter);

        return result != null;
    }


    public async Task<List<UserClaims>> RetrieveFromUser(User user)
    {
        var filter = Builders<UserClaims>.Filter.Eq(uc => uc.UserId, user.Id);

        return await dbService.RetrieveMultipleAsync(userClaimCollection, filter);
    }

    public async Task<UserClaims?> RetrieveFromUserAndProfile(ObjectId userId, ObjectId profileId)
    {
        var filter = Builders<UserClaims>.Filter.And(
            Builders<UserClaims>.Filter.Eq(uc => uc.UserId, userId),
            Builders<UserClaims>.Filter.Eq(uc => uc.ProfileId, profileId)
        );

        return await dbService.RetrieveOrNullAsync(userClaimCollection, filter);
    }

    public async Task<UserClaims> SetRole(User user, Profile profile, PresetClaimRole role, IClientSessionHandle session)
    {
        var adminClaimTypes = PresetClaimRoleMapper.GetClaimsForRole(role);

        var adminClaims = adminClaimTypes
            .Select(claimType => new UserClaim(claimType))
            .ToHashSet();

        var userClaims = new UserClaims(user, profile)
        {
            Claims = adminClaims
        };

        await dbService.CreateOneAsync(userClaimCollection, userClaims, session);

        return userClaims;
    }



}