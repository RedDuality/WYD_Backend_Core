using Core.Components.Database;
using MongoDB.Driver;
using Core.DTO.UserAPI;
using Core.Model.Users;
using Core.Model.Profiles;
using Core.Services.Profiles;

namespace Core.Services.Users;


public class UserService(MongoDbService dbService, ProfileService profileService)
{

    private readonly CollectionName userCollection = CollectionName.Users;

    private async Task<User?> RetrieveByAccountUid(string accountUid)
    {

        var filter = Builders<User>.Filter.ElemMatch(
            u => u.Accounts,
            a => a.Uid == accountUid);

        var users = await dbService.RetrieveMultipleAsync(userCollection, filter);

        User? user = users.FirstOrDefault();

        return user;
    }

    public async Task<User> GetOrCreateAsync(string uid, string? email)
    {
        var user = await RetrieveByAccountUid(uid);

        return user ?? await CreateUserAsync(uid, email);
    }

    private async Task<User> CreateUserAsync(string accountUid, string? email)
    {
        string mail = email ??
            throw new UnauthorizedAccessException("No Email in the claims");

        return await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            var user = new User(new Account(accountUid, mail));

            await dbService.CreateOneAsync(userCollection, user, session);

            var profile = await profileService.CreateAsync(accountUid, mail, session);

            await AddProfileAsync(profile, user, session, UserProfileRole.SuperAdmin, true);

            return user;
        });
    }

    public async Task<User> AddProfileAsync(Profile profile, User user, IClientSessionHandle session, UserProfileRole userRole = UserProfileRole.Viewer, bool mainProfile = false)
    {
        var userProfile = new UserProfile(profile, userRole, mainProfile);

        var userUpdate = Builders<User>.Update.Push(u => u.Profiles, userProfile);
        user = await dbService.FindOneByIdAndUpdateAsync(userCollection, user.Id, userUpdate, session);

        await profileService.AddUserAsync(profile, user, session);
        return user;
    }

    public async Task<RetrieveUserResponseDto> RetrieveProfilesAsync(User user)
    {
        var profileIds = user.Profiles.Select(up => up.ProfileId).ToHashSet();
        var profiles = await dbService.RetrieveMultipleByIdAsync<Profile>(CollectionName.Profiles, profileIds);

        // Map the results together
        var userProfilesDictionary = user.Profiles.ToDictionary(d => d.ProfileId);
        var userProfiles = profiles.Select(p => new Tuple<Profile, UserProfile>(p, userProfilesDictionary[p.Id])).ToList();
        
        return new RetrieveUserResponseDto(user, userProfiles);
    }
}
/*
    public static async Task<List<EventDto>> RetrieveEventsAsync(User user)
    {
        var tasks = user.Profiles.Select(async profile =>
        {
            try
            {
                return await Task.FromResult(ProfileService.RetrieveEvents(profile));
            }
            catch (Exception ex)
            {
                // In case of an error, return a single EventDto with an error message
                return [
                new( new Event {
                    Id = -1,
                    Title = $"Error retrieving events for profile {profile.Id}",
                    Description = ex.Message,
                    StartTime = DateTimeOffset.MinValue,
                    EndTime = DateTimeOffset.MinValue,
                })];
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(result => result).ToList();
    }

    public void SetProfileRole(User user, Profile profile, Role role)
    {
        if (user == null) throw new ArgumentNullException(nameof(user), "User cannot be null.");
        if (profile == null) throw new ArgumentNullException(nameof(profile), "Profile cannot be null.");

        var userRole = user.UserRoles.Find(ur => ur.Profile == profile);
        if (userRole == null)
        {
            throw new KeyNotFoundException("User does not have the specified profile.");
        }

        userRole.Role = role;

        try
        {
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error updating user profile role.", ex);
        }
    }

    public void UdateProfileColor(User user, ProfileDto profileDto)
    {
        if (user == null) throw new ArgumentNullException(nameof(user), "User cannot be null.");


        var userRole = user.UserRoles.Find(ur => ur.Profile.Hash == profileDto.Hash) ?? throw new KeyNotFoundException("User does not have the specified profile.");
        userRole.Color = profileDto.Color ?? throw new ArgumentNullException("Cannot update to a null color.");

        try
        {
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error updating user profile role.", ex);
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
        *//*
} */