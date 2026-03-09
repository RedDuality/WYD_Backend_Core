using Core.Components.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using Core.Model.Profiles;
using Core.Model.Events.Recurrence;
using Core.Services.Events.Recurrence;


namespace Core.Services.Profiles;

public class ProfileRecurrentEventService(
    MongoDbService dbService, 
    RecurrentEventProfileService eventProfileService)
{
    private readonly CollectionName profileRecurrentEventCollection = CollectionName.ProfileRecurrentEvents;

    public async Task<ProfileRecurrentEvent> CreateProfileEventAsync(RecurrentEvent ev, ObjectId profileId, IClientSessionHandle session)
    {
        ProfileRecurrentEvent profileEvent = new(
                ev,
                profileId
            );

        await dbService.CreateOneAsync(profileRecurrentEventCollection, profileEvent, session);

        await eventProfileService.CreateEventProfileAsync(profileEvent, session);
        return profileEvent;
    }

    

    public async Task PropagateEventUpdatesAsync(
        RecurrentEvent ev,
        IEnumerable<ObjectId> profileIds,
        IClientSessionHandle? session = null)
    {
        var filter = Builders<ProfileRecurrentEvent>.Filter.And(
            Builders<ProfileRecurrentEvent>.Filter.In(pe => pe.ProfileId, profileIds),
            Builders<ProfileRecurrentEvent>.Filter.Eq(pe => pe.EventId, ev.Id),
            // given the asyncronicity, another later update could have already happened
            Builders<ProfileRecurrentEvent>.Filter.Lt(pe => pe.EventUpdatedAt, ev.UpdatedAt)
        );

        var update = Builders<ProfileRecurrentEvent>.Update
            .Set(pe => pe.EventUpdatedAt, ev.UpdatedAt)
            .Set(pe => pe.RecurrenceStart, ev.StartTime)
            .Set(pe => pe.RecurrenceEnd, ev.RecurrenceEnd);

        var result = await dbService.UpdateManyAsync(profileRecurrentEventCollection, filter, update, session: session);

        //Console.WriteLine($"Matched {result.MatchedCount}, Modified {result.ModifiedCount}");
    }
}