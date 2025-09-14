using Core.Model;
using Core.Model.Details;
using Core.Components.Database;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Core.Services.Model;

public class EventDetailsService(MongoDbService dbService)
{
    private readonly CollectionName eventDetailsCollection = CollectionName.EventDetails;

    public async Task<EventDetails> CreateAsync(Event ev, string? description, IClientSessionHandle session)
    {
        var eventDetails = new EventDetails(ev)
        {
            Description = description
        };
        await dbService.CreateOneAsync(eventDetailsCollection, eventDetails, session);

        return eventDetails;
    }

    public async Task<EventDetails> RetrieveByEventId(string eventId)
    {
        var filter = Builders<EventDetails>.Filter.Eq(ed => ed.EventId, new ObjectId(eventId));
        return await dbService.FindOneAsync(eventDetailsCollection, filter);
    }
}
