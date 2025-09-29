using MongoDB.Driver;
using MongoDB.Bson;
using Core.Components.Database;
using Core.Model.Profiles;
using Core.Model.Events;

namespace Core.Services.Events;

public class EventProfileService(MongoDbService dbService)
{
    private readonly CollectionName eventProfileCollection = CollectionName.EventProfiles;

    public async Task<EventProfile> CreateEventProfileAsync(EventProfile eventProfile, IClientSessionHandle session)
    {
        return await dbService.CreateOneAsync(eventProfileCollection, eventProfile, session);
    }

    public async Task<List<EventProfile>> FindAllByEventId(string eventId)
    {
        var result = await dbService.RetrieveMultipleAsync(
            eventProfileCollection,
            Builders<EventProfile>.Filter.Eq(ep => ep.EventId, new ObjectId(eventId)));

        var eventProfiles = result?.ToList();
        if (eventProfiles == null || eventProfiles.Count == 0)
        {
            throw new InvalidOperationException($"No event profiles found for EventId: {eventId}");
        }

        return eventProfiles;
    }

}
