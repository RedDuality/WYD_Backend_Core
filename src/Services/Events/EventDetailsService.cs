using Core.Components.Database;
using MongoDB.Driver;
using MongoDB.Bson;
using Core.Model.Events;

namespace Core.Services.Events;

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
        return await dbService.RetrieveAsync(eventDetailsCollection, filter);
    }

    public async Task AddImages(int added, string eventId)
    {
        var detailsFilter = Builders<EventDetails>.Filter.Eq(ed => ed.EventId, new ObjectId(eventId));
        var updateDefinition = Builders<EventDetails>.Update.Inc(ed => ed.TotalImages, added);
        await dbService.UpdateOneAsync(eventDetailsCollection, detailsFilter, updateDefinition, null);
    }

    public async Task<EventDetails> Update(ObjectId eventId, string description, IClientSessionHandle session)
    {
        var detailsFilter = Builders<EventDetails>.Filter.Eq(ed => ed.EventId, eventId);
        var updateDefinition = Builders<EventDetails>.Update.Set(ed => ed.Description, description);
        return await dbService.FindOneAndUpdateAsync(eventDetailsCollection, detailsFilter, updateDefinition, session);
    }

}
