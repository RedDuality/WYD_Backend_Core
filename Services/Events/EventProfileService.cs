using MongoDB.Driver;
using MongoDB.Bson;
using Core.Components.Database;
using Core.Model.Profiles;
using Core.Model.Events;
using Core.Services.Notifications;

namespace Core.Services.Events;

public class EventProfileService(MongoDbService dbService) : IProfileFinder
{
    private readonly CollectionName eventProfileCollection = CollectionName.EventProfiles;

    public async Task<EventProfile> CreateEventProfileAsync(ProfileEvent profileEvent, IClientSessionHandle? session)
    {
        var eventProfile = new EventProfile(profileEvent);
        await dbService.CreateOneAsync(eventProfileCollection, eventProfile, session);
        return eventProfile;
    }

    public async Task<List<EventProfile>> CreateMultipleEventProfileAsync(List<ProfileEvent> profileEvents, IClientSessionHandle session)
    {
        var eventProfiles = profileEvents
            .Select(pe => new EventProfile(pe))
            .ToList();

        return await dbService.CreateManyAsync(eventProfileCollection, eventProfiles, session);
    }

    public async Task<List<ObjectId>> FindAlreadyExisting(Event ev, HashSet<ObjectId> profileIds)
    {
        var filter = Builders<EventProfile>.Filter.And(
            Builders<EventProfile>.Filter.Eq(ep => ep.EventId, ev.Id),
            Builders<EventProfile>.Filter.In(ep => ep.ProfileId, profileIds)
        );

        var existingEventProfiles = await dbService.RetrieveMultipleAsync(eventProfileCollection, filter);

        return existingEventProfiles
            .Select(ep => ep.ProfileId)
            .Distinct()
            .ToList();
    }


    private async Task<List<EventProfile>> FindAllByEventId(ObjectId eventId)
    {
        var result = await dbService.RetrieveMultipleAsync(
            eventProfileCollection,
            Builders<EventProfile>.Filter.Eq(ep => ep.EventId, eventId));

        var eventProfiles = result?.ToList();
        if (eventProfiles == null || eventProfiles.Count == 0)
        {
            throw new InvalidOperationException($"No event profiles found for EventId: {eventId}");
        }

        return eventProfiles;
    }

    public async Task<List<ObjectId>> GetProfileIdsAsync(ObjectId eventId)
    {
        var eps = await FindAllByEventId(eventId);
        return eps.Select(ep => ep.ProfileId).ToList();
    }

}
