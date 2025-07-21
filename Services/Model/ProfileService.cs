using Model;
using Database;
using Dto;

namespace Service;
public class ProfileService(WydDbContext wydDbContext)
{

    readonly WydDbContext db = wydDbContext;

    public Profile? RetrieveOrNull(int id)
    {
        return db.Profiles.Find(id);

    }

    public Profile Retrieve(int id)
    {
        return db.Profiles.Find(id) ?? throw new KeyNotFoundException("Profile");
    }

    public Profile RetrieveByHash(string hash)
    {
        return db.Profiles.Where(p => p.Hash.Equals(hash)).First() ?? throw new KeyNotFoundException("Profile");
    }


    public HashSet<ProfileDto> RetrieveProfiles(HashSet<string> profileHashes)
    {
        return db.Profiles.Where(p => profileHashes.Contains(p.Hash)).Select(p => new ProfileDto(p)).ToHashSet();
    }

    public Profile Create(Profile profile)
    {
        db.Profiles.Add(profile);
        db.SaveChanges();
        return profile;
    }

    public static List<EventDto> RetrieveEvents(Profile profile)
    {
        return profile.Events.Select(ev => new EventDto(ev)).ToList();
    }

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

}