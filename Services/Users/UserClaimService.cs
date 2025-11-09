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


    public async Task<UserClaims> SetAdmin(User user, Profile profile)
    {
        // 1. Get the admin claim types
        var adminClaimTypes = GetAdminClaims();

        // 2. Wrap them into UserClaim objects
        var adminClaims = adminClaimTypes
            .Select(claimType => new UserClaim(claimType))
            .ToHashSet();

        // 3. Build the UserClaims document
        var userClaims = new UserClaims(user, profile)
        {
            Claims = adminClaims
        };

        // 4. Persist to MongoDB
        await dbService.CreateOneAsync(userClaimCollection, userClaims, session: null);

        // 5. Return the created document
        return userClaims;
    }


    private static HashSet<UserClaimType> GetAdminClaims()
    {
        return [
            UserClaimType.CanViewProfileDetails,
            UserClaimType.CanImpersonateProfile,
            UserClaimType.CanViewCommunity,
            UserClaimType.CanCreateCommunity,
            UserClaimType.CanEditCommunity,
            UserClaimType.CanReadEvents,
            UserClaimType.CanCreateEvents,
            UserClaimType.CanEditEvents,
            UserClaimType.CanShareEvents
        ];
    }



}