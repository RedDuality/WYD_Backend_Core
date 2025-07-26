/* using Model;
using Database;
using Dto;

namespace Service;


public class UserService(WydDbContext context, AccountService accountService, ProfileService profileService, IAuthenticationService authenticationService)
{
    private readonly WydDbContext db = context ?? throw new ArgumentNullException(nameof(context), "Database context cannot be null.");
    private readonly AccountService _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService), "Account service cannot be null.");
    private readonly ProfileService _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService), "Profile service cannot be null.");

    private readonly IAuthenticationService _authenticationService = authenticationService;

    public User? RetrieveOrNull(int id)
    {
        return db.Users.Find(id);
    }

    public async Task<User> GetOrCreateAsync(string uid)
    {
        Account? account = _accountService.Retrieve(uid);

        if (account == null) //registration
        {
            UserRecord UR = await _authenticationService.RetrieveAccount(uid);
            return Create(UR.Email, UR.Uid);
        }
        return account.User;
    }

//TODO make private
    public User Create(string Email, string Uid)
    {
        User user;
        using var transaction = db.Database.BeginTransaction();
        try
        {
            // Create a new user
            user = new User();
            db.Users.Add(user);
            db.SaveChanges();

            // Create associated account
            var account = new Account
            {
                Mail = Email,
                Uid = Uid,
                User = user
            };

            _accountService.Create(account);

            // Create associated profile
            var profile = new Profile()
            {
                Name = Email,
                Tag = Email
            };

            AddProfile(user, profile);

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new InvalidOperationException("Error creating user " + ex.Message);
        }
        return user;
    }

    public User AddProfile(User user, Profile profile)
    {
        if (user == null) throw new ArgumentNullException(nameof(user), "User cannot be null.");
        if (profile == null) throw new ArgumentNullException(nameof(profile), "Profile cannot be null.");

        try
        {
            _profileService.Create(profile);
            user.MainProfile = profile;
            user.Profiles.Add(profile);
            db.SaveChanges();
            return user;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error adding profile to user.", ex);
        }
    }

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