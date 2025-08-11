using Core.Model;
using Core.Model.Join;
using Core.Services.Database;
using MongoDB.Driver;

namespace Core.Services.Model;

public class ProfileService(MongoDbService dbService, ProfileDetailsService profileDetailsService)
{
    private readonly CollectionName profileCollection = CollectionName.Profiles;

    public async Task<Profile> CreateAsync(string tag, string name, User user, IClientSessionHandle session)
    {
        var profile = new Profile
        {
            Tag = tag,
            Name = name
        };

        await dbService.CreateOneAsync(profileCollection, profile, session);

        await profileDetailsService.CreateAsync(profile, user, session);
        return profile;
    }

    public async Task<Profile> AddUserAsync(Profile profile, User user, IClientSessionHandle session)
    {
        var userProfile = new UserProfile(profile);

        var userUpdate = Builders<User>.Update.Push(u => u.Profiles, userProfile);
        await dbService.PatchUpdateAsync(CollectionName.Users, user.Id, userUpdate, session);

        await profileDetailsService.AddUser(profile.DetailsId, user, session);
        return profile;
    }

}
/*


    public Profile UpdateAsync(ProfileDto dto)
    {
        Profile profileToUpdate;
        try
        {
            profileToUpdate = RetrieveByHash(dto.Hash!);
            profileToUpdate.Update(dto);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error updating event", ex);
        }

        //TODO check for removed images

        return profileToUpdate;

    }

    public void SetEventRole(Event ev, Profile profile, EventRole role)
    {
        var profileEvent = profile.ProfileEvents.Find(pe => pe.Event.Id == ev.Id) ?? throw new KeyNotFoundException("ProfileEvent");

        profileEvent.Role = role;

        db.SaveChanges();
    }

    public List<ProfileDto> SearchByTag(string searchTag)
    {
        return db.Profiles
                 .Where(p => p.Tag.StartsWith(searchTag))
                 .Take(5).Select(p => new ProfileDto(p)).ToList();
    }

} */