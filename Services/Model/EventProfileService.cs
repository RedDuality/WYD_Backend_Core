using MongoDB.Driver;
using MongoDB.Bson;
using Core.Services.Util;
using Core.Model;

namespace Core.Services.Model;

public class EventProfileService(MongoDbService dbService)
{
    private readonly string collectionName = "EventProfiles";

    public async Task<EventProfile> CreateEventProfileAsync(EventProfile eventProfile, IClientSessionHandle session)
    {
        return await dbService.CreateOneAsync(collectionName, eventProfile, session);
    }

    public async Task<List<EventProfile>> FindAllByEventId(string eventId)
    {
        var result = await dbService.FindAsync(
            collectionName,
            Builders<EventProfile>.Filter.Eq(ep => ep.EventId, new ObjectId(eventId)));

        var eventProfiles = result?.ToList();
        if (eventProfiles == null || eventProfiles.Count == 0)
        {
            throw new InvalidOperationException($"No event profiles found for EventId: {eventId}");
        }

        return eventProfiles;
    }

}
