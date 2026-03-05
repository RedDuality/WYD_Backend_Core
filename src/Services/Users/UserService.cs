using Core.Components.Database;
using MongoDB.Driver;
using Core.DTO.UserAPI;
using Core.Model.Users;
using Core.Model.Profiles;
using Core.Services.Profiles;
using MongoDB.Bson;
using Core.Services.Util;
using Core.External.Interfaces;

namespace Core.Services.Users;


public class UserService(
    MongoDbService dbService,
    ProfileService profileService,
    UserProfileService userProfileService,
    UserClaimService userClaimService,
    IAuthService authService,
    IContextManager contextManager)
{

    private readonly CollectionName userCollection = CollectionName.Users;

    public async Task<RetrieveUserResponseDto> Login()
    {
        var accountUid = contextManager.GetAccountId();
        var userId = contextManager.TryGetUserId();

        // when the user tried to register, firebase was on but the server on down, need to create the new user
        if (userId == null)
        {
            return await Register(accountUid);
        }

        return await RetrieveUserWithProfiles(userId, accountUid);
    }

    private async Task<RetrieveUserResponseDto> RetrieveUserWithProfiles(string userId, string accountUid)
    {
        var user = await RetrieveUser(userId, accountUid);
        // somehow the db was destroyed, but firebase still had in memory the userId
        if (user == null)
        {
            var returnDto = await Register(accountUid);
            await authService.RevokeTokens(accountUid);
            return returnDto;
        }
        var tuple = await profileService.RetrieveDetailedProfilesAsync(user.Id, user.ProfileIds);

        return new RetrieveUserResponseDto(user, tuple);
    }

    private async Task<User?> RetrieveUser(string userId, string accountUid)
    {
        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, new ObjectId(userId)),
            Builders<User>.Filter.ElemMatch(
                u => u.Accounts,
                a => a.Uid == accountUid)
            );

        var users = await dbService.RetrieveMultipleAsync(userCollection, filter);

        return users.FirstOrDefault();
    }



    public async Task<RetrieveUserResponseDto> Register()
    {
        string accountUid = contextManager.GetAccountId();
        return await Register(accountUid);
    }

    private async Task<RetrieveUserResponseDto> Register(string accountUid)
    {
        string email = contextManager.GetEmail();
        SignInType signInType = contextManager.GetSignInType();

        return await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            var user = new User(new Account(accountUid, email, signInType));
            await dbService.CreateOneAsync(userCollection, user, session);

            var profile = await profileService.CreateAsync(accountUid, email, session);
            var result = await AddProfileAsync(profile, user, session, true, PresetClaimRole.Admin);

            // updates the Auth Provider to include the userId in the claims
            var claims = new Dictionary<string, string>
            {
                ["userId"] = user.Id.ToString()
            };
            await authService.AddOrUpdateClaimsAsync(accountUid, claims);

            return new RetrieveUserResponseDto(result.UpdatedUser, [Tuple.Create(result.Profile, result.UserProfile, result.UserClaims, result.profileDetails)]);
        });
    }

    public async Task<(User UpdatedUser, Profile Profile, UserProfile UserProfile, UserClaims UserClaims, ProfileDetails profileDetails)> AddProfileAsync(
        Profile profile,
        User user,
        bool mainProfile = false,
        PresetClaimRole role = PresetClaimRole.Viewer)
    {
        return await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            return await AddProfileAsync(profile, user, session, mainProfile, role);
        });
    }

    public async Task<(User UpdatedUser, Profile Profile, UserProfile UserProfile, UserClaims UserClaims, ProfileDetails profileDetails)> AddProfileAsync(
        Profile profile,
        User user,
        IClientSessionHandle session,
        bool mainProfile = false,
        PresetClaimRole role = PresetClaimRole.Viewer)
    {
        user = await AddProfileToUserDoc(user, profile, mainProfile, session);

        var userProfile = await userProfileService.Create(user, profile, session);


        var profileDetails = await profileService.AddUserAsync(profile, user, session);

        var userClaims = await userClaimService.SetRole(user, profile, role, session);

        return (user, profile, userProfile, userClaims, profileDetails);
    }

    private async Task<User> AddProfileToUserDoc(User user, Profile profile, bool mainProfile, IClientSessionHandle session)
    {
        var updates = new List<UpdateDefinition<User>>
        {
            Builders<User>.Update.AddToSet(u => u.ProfileIds, profile.Id)
        };
        if (mainProfile)
        {
            updates.Add(Builders<User>.Update.Set(u => u.MainProfileId, profile.Id));
        }
        var update = Builders<User>.Update.Combine(updates);
        return await dbService.FindOneByIdAndUpdateAsync(userCollection, user.Id, update, session);
    }

    public async Task<HashSet<ObjectId>> GetProfileIds(string userId)
    {
        var user = await dbService.RetrieveByIdAsync<User>(userCollection, userId);

        return user.ProfileIds;
    }

    public async Task<User> CheckUserHaveAccount(string userId, string accountEmail)
    {
        var user = await dbService.RetrieveByIdAsync<User>(userCollection, userId);

        if (user.Accounts.Where((a) => a.Email == accountEmail).ToList().Count > 0)
        {
            return user;
        }
        throw new UnauthorizedAccessException("User does not have permission over the account");
    }
}

/*
    public string Delete(int id)
    {

        User user = db.Users.Include(u => u.Events).ThenInclude(e => e.UserEvents).Single(u => u.Id == id);
        List<Event> orphanEvents = user.Events.Where(e => e.UserEvents.Count == 1).ToList();
        db.Remove(user);
        db.Events.RemoveRange(orphanEvents);
        db.SaveChanges();
        return "Utente eliminato con successo";

    }
    */