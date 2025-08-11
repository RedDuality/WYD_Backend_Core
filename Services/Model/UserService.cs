
using Core.Model;
using Core.Services.Database;
using Core.Services.Interfaces;
using Core.Services.Model;
using MongoDB.Driver;

namespace Service;


public class UserService(MongoDbService dbService, ProfileService profileService, IAuthenticationService authenticationService)
{

    private readonly CollectionName userCollection = CollectionName.Users;

    private async Task<User?> RetrieveByAccountUid(string accountUid)
    {

        var filter = Builders<User>.Filter.ElemMatch(
            u => u.Accounts,
            a => a.Uid == accountUid);

        var users = await dbService.FindAsync(userCollection, filter);

        User? user = users.FirstOrDefault();

        return user;
    }

    public async Task<User> GetOrCreateAsync(string uid)
    {
        var user = await RetrieveByAccountUid(uid);

        return user ?? await CreateUserAsync(uid);
    }

    private async Task<User> CreateUserAsync(string accountUid)
    {
        UserRecord UR = await authenticationService.RetrieveAccount(accountUid);

        User user = await dbService.ExecuteInTransactionAsync(async (session) =>
        {
            var newAccount = new Account(UR.Uid, UR.Email);

            var newUser = new User
            {
                Accounts = { newAccount }
            };

            var user = await dbService.CreateOneAsync(userCollection, newUser, session);

            var profile = await profileService.CreateAsync(UR.Email, UR.Email, user, session);

            await profileService.AddUserAsync(profile, user, session);

            return user;
        });

        return user;
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
                    StartTime = DateTime.MinValue,
                    EndTime = DateTime.MinValue,
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