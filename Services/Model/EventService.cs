using System.Collections.Concurrent;
using Azure.Storage.Blobs;
using Database;
using Dto;
using Model;

namespace Service;

public class EventService(
    WydDbContext context,
    GroupService groupService,
    ProfileService profileService
)
{
    private readonly WydDbContext db =
        context
        ?? throw new ArgumentNullException(nameof(context), "Database context cannot be null");

    private readonly GroupService groupService =
        groupService
        ?? throw new ArgumentNullException(nameof(context), "GroupService cannot be null");

    private readonly ProfileService profileService = profileService;

    public Event RetrieveByHash(string eventHash)
    {
        return db.Events.FirstOrDefault(e => e.Hash == eventHash)
            ?? throw new KeyNotFoundException($"Event with ID {eventHash} not found.");
    }

    public Event SharedWithHash(string eventHash, Profile profile)
    {
        if (string.IsNullOrEmpty(eventHash))
        {
            throw new ArgumentException("Event hash cannot be null or empty.", nameof(eventHash));
        }

        Event ev = RetrieveByHash(eventHash);

        ev.Profiles.Add(profile);
        db.SaveChanges();

        return ev;
    }

    private Event CreateNewAndSave(EventDto eventDto)
    {
        Event newEvent = Event.FromDto(eventDto);

        db.Events.Add(newEvent);
        db.SaveChanges();
        return newEvent;
    }

    public Event Create(EventDto dto, Profile profile)
    {
        Event newEvent;
        using var transaction = db.Database.BeginTransaction();
        try
        {
            newEvent = CreateNewAndSave(dto);
            Share(newEvent, [profile]); //add profile to event

            if (dto.ProfileEvents.Count != 0 && dto.ProfileEvents.First().Confirmed)
                ConfirmOrDecline(newEvent, profile, true);

            profileService.SetEventRole(newEvent, profile, EventRole.Owner);

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new InvalidOperationException(
                "Error creating event. Transaction rolled back.",
                ex
            );
        }

        return newEvent;
    }

    public Event UpdateAsync(EventDto dto)
    {
        Event eventToUpdate;
        try
        {
            eventToUpdate = RetrieveByHash(dto.Hash!);
            eventToUpdate.Update(dto);
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error updating event", ex);
        }

        //TODO check for removed images

        return eventToUpdate;
    }

    public async Task<Event> AddMultipleBlobs(string eventHash, HashSet<BlobData> blobDatas)
    {
        Event ev = RetrieveByHash(eventHash);
        await AddMultipleBlobs(ev, blobDatas);
        return ev;
    }

    private async Task AddMultipleBlobs(Event ev, HashSet<BlobData> blobDatas)
    {
        var savedImagesCount = blobDatas.Count;

        if (savedImagesCount > 0)
        {
            try
            {
                var container = await BlobService.GetContainerClientAsync(ev.Hash);
                foreach (var bd in blobDatas)
                {
                    var newBlob = AddBlob(container, bd);
                    ev.Blobs.Add(newBlob);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error saving images: ", ex);
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch
            {
                throw new ThreadInterruptedException("No images were saved");
            }
        }
    }

    private static Blob AddBlob(BlobContainerClient blobContainerClient, BlobData blobData)
    {
        Blob newBlob = new();
        string extension = BlobService.UploadBlob(blobContainerClient, newBlob, blobData);
        newBlob.Hash += extension;
        return newBlob;
    }

    internal Event ShareToGroups(string eventHash, HashSet<int> groupIds)
    {
        Event ev = RetrieveByHash(eventHash);

        var groups = groupService.Retrieve(groupIds).ToList();
        var profiles = groups.SelectMany(g => g.Profiles).ToHashSet();

        if (profiles.Count == 0)
            throw new Exception("No Profile to add this event to!");

        return Share(ev, profiles!);
    }

    public Event Share(Event ev, HashSet<Profile> profiles)
    {
        ev.Profiles.UnionWith(profiles);

        try
        {
            db.SaveChanges();
            return ev;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error sharing event with profiles.", ex);
        }
    }

    public Event ConfirmOrDecline(string eventHash, Profile profile, bool confirmed)
    {
        Event ev = RetrieveByHash(eventHash);
        return ConfirmOrDecline(ev, profile, confirmed);
    }

    public Event ConfirmOrDecline(Event ev, Profile profile, bool confirmed)
    {
        //TODO check the start time is not after now
        var profileEvent =
            ev.ProfileEvents.Find(pe => pe.Profile.Id == profile.Id)
            ?? throw new KeyNotFoundException(
                $"ProfileEvent with Profile ID {profile.Id} not found for the given profile."
            );
        profileEvent.Confirmed = confirmed;

        try
        {
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Error confirming or declining event for profile.",
                ex
            );
        }
        return ev;
    }

    public Event? DeleteForProfile(string eventHash, Profile profile)
    {
        Event ev = RetrieveByHash(eventHash);
        ev.Profiles.Remove(profile);
        if (ev.Profiles.Count == 0)
        {
            db.Remove(ev);
            db.SaveChanges();
            return null;
        }
        db.SaveChanges();
        return ev;
    }

    /*
        public HashSet<Profile> Delete(int id, int userId)
        {
            Event ev = db.Events.Single(e => e.Id == id);
            if (ev.OwnerId != userId)
                return false;
            db.Remove(ev);
            db.SaveChanges();
            return true;

        }
*/
}
