using Core.Components.Database;
using MongoDB.Driver;
using Core.DTO.UserAPI;
using Core.Model.Users;
using Core.Model.Profiles;
using Core.Services.Profiles;
using Core.External.Authentication;
using MongoDB.Bson;

namespace Core.Services.Users;


public class UserService(MongoDbService dbService, ProfileService profileService, UserClaimService userClaimService, FirebaseAuthService authService)
{

    private readonly CollectionName userCollection = CollectionName.Users;


    public async Task<User> Retrieve(string userId, string accountUid)
    {

        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(u => u.Id, new ObjectId(userId)),
            Builders<User>.Filter.ElemMatch(
                u => u.Accounts,
                a => a.Uid == accountUid)
            );

        var users = await dbService.RetrieveMultipleAsync(userCollection, filter);


        return users.FirstOrDefault() ?? throw new UnauthorizedAccessException("The account has no associated user");
    }

    public async Task<RetrieveUserResponseDto> RetrieveWithProfiles(string userId, string accountUid)
    {

        var user = await Retrieve(userId, accountUid);

        return await RetrieveProfilesAsync(user);
    }

    private async Task<RetrieveUserResponseDto> RetrieveProfilesAsync(User user)
    {
        var profileIds = user.Profiles.Select(up => up.ProfileId).ToHashSet();
        var profiles = await dbService.RetrieveMultipleByIdAsync<Profile>(CollectionName.Profiles, profileIds);

        // Map the results together
        var userProfilesDictionary = user.Profiles.ToDictionary(d => d.ProfileId);
        var userProfiles = profiles.Select(p => new Tuple<Profile, UserProfile>(p, userProfilesDictionary[p.Id])).ToList();

        return new RetrieveUserResponseDto(user, userProfiles);
    }


    public async Task<User> CreateUserAsync(string accountUid, string email)
    {
        return await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            var user = new User(new Account(accountUid, email));

            await dbService.CreateOneAsync(userCollection, user, session);

            var profile = await profileService.CreateAsync(accountUid, email, session);

            await AddProfileAsync(profile, user, session, true);

            var useClaims = await userClaimService.SetAdmin(user, profile);

            await authService.AddOrUpdateUserIdClaimAsync(accountUid, user.Id.ToString());

            return user;
        });
    }

    public async Task<User> AddProfileAsync(Profile profile, User user, IClientSessionHandle session, bool mainProfile = false)
    {
        var userProfile = new UserProfile(profile, mainProfile);

        var userUpdate = Builders<User>.Update.Push(u => u.Profiles, userProfile);
        user = await dbService.FindOneByIdAndUpdateAsync(userCollection, user.Id, userUpdate, session);

        await profileService.AddUserAsync(profile, user, session);
        return user;
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