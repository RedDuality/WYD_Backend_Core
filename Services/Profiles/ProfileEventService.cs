using Core.Components.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using Core.Services.Events;
using Core.Model.Profiles;
using Core.Model.Events;
using Core.DTO.EventAPI;


namespace Core.Services.Profiles;

public class ProfileEventService(MongoDbService dbService, EventProfileService eventProfileService)
{
    private readonly CollectionName profileEventCollection = CollectionName.ProfileEvents;

    public async Task<ProfileEvent> CreateProfileEventAsync(Event ev, ObjectId profileId, IClientSessionHandle session, bool confirmed = true)
    {
        ProfileEvent profileEvent = new(
                ev,
                profileId
            )
        {
            Confirmed = confirmed,
        };
        await dbService.CreateOneAsync(profileEventCollection, profileEvent, session);

        await eventProfileService.CreateEventProfileAsync(profileEvent, session);
        return profileEvent;
    }

    public async Task<List<ProfileEvent>> CreateMultipleProfileEventAsync(Event ev, HashSet<ObjectId> profileIds, IClientSessionHandle session, bool confirmed = false)
    {
        var profileEvents = new List<ProfileEvent>();

        // Step 1: Create ProfileEvent objects
        foreach (var profileId in profileIds)
        {
            var profileEvent = new ProfileEvent(ev, profileId)
            {
                Confirmed = confirmed
            };
            profileEvents.Add(profileEvent);
        }

        // Step 2: Insert ProfileEvents to get their IDs
        await dbService.CreateManyAsync(profileEventCollection, profileEvents, session);

        // Step 3: Create EventProfiles using inserted ProfileEvents (with IDs)
        await eventProfileService.CreateMultipleEventProfileAsync(profileEvents, session);

        return profileEvents;

    }

    public async Task PropagateEventUpdatesAsync(
        Event ev,
        IEnumerable<ObjectId> profileIds,
        IClientSessionHandle? session = null)
    {
        var filter = Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.In(pe => pe.ProfileId, profileIds),
            Builders<ProfileEvent>.Filter.Eq(pe => pe.EventId, ev.Id),
            // given the asyncronicity, another later update could have already happened
            Builders<ProfileEvent>.Filter.Lt(pe => pe.EventUpdatedAt, ev.UpdatedAt)
        );

        var update = Builders<ProfileEvent>.Update
            .Set(pe => pe.EventUpdatedAt, ev.UpdatedAt)
            .Set(pe => pe.EventStartTime, ev.StartTime)
            .Set(pe => pe.EventEndTime, ev.EndTime);

        // also updated updatedAt date
        var result = await dbService.UpdateManyAsync(profileEventCollection, filter, update, session: session);

        Console.WriteLine($"Matched {result.MatchedCount}, Modified {result.ModifiedCount}");
    }


    public async Task<ProfileEvent?> FindByProfileAndEventId(string profileId, string eventId)
    {
        var filter = Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.Eq(doc => doc.ProfileId, new ObjectId(profileId)),
            Builders<ProfileEvent>.Filter.Eq(doc => doc.EventId, new ObjectId(eventId))
        );
        return await dbService.RetrieveOrNullAsync(profileEventCollection, filter);
    }

    public async Task<HashSet<ProfileEventDto>> FindMultipleByProfileAndEventIds(IEnumerable<(string profileId, string eventId)> profileEventPairs)
    {
        var filters = new List<FilterDefinition<ProfileEvent>>();
        foreach (var (profileId, eventId) in profileEventPairs)
        {
            var profileObjectId = new ObjectId(profileId);
            var eventObjectId = new ObjectId(eventId);

            var filter = Builders<ProfileEvent>.Filter.And(
                Builders<ProfileEvent>.Filter.Eq(doc => doc.ProfileId, profileObjectId),
                Builders<ProfileEvent>.Filter.Eq(doc => doc.EventId, eventObjectId)
            );

            filters.Add(filter);
        }
        var combinedFilter = Builders<ProfileEvent>.Filter.Or(filters);

        var projection = Builders<ProfileEvent>.Projection.Expression(pe => new ProfileEventDto
        {
            ProfileId = pe.ProfileId.ToString(),
            Role = pe.Role,
            Confirmed = pe.Confirmed,
            Trusted = false
        });

        var results = await dbService.RetrieveProjectedAsync(profileEventCollection, combinedFilter, projection);

        return results.ToHashSet();
    }

    public async Task<ProfileEvent?> FindByEventId(string eventId)
    {
        var filter = Builders<ProfileEvent>.Filter.Eq(doc => doc.EventId, new ObjectId(eventId));

        return await dbService.RetrieveOrNullAsync(profileEventCollection, filter);
    }

    public async Task<bool> Confirm(string profileId, string eventId, IClientSessionHandle session)
    {
        var filter = Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.Eq(doc => doc.ProfileId, new ObjectId(profileId)),
            Builders<ProfileEvent>.Filter.Eq(doc => doc.EventId, new ObjectId(eventId)),
            Builders<ProfileEvent>.Filter.Ne(doc => doc.Confirmed, true) // Only update if not already confirmed
        );

        var confirmUpdate = Builders<ProfileEvent>.Update.Set(pe => pe.Confirmed, true);

        var result = await dbService.UpdateOneAsync(CollectionName.ProfileEvents, filter, confirmUpdate, session);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> Decline(string profileId, string eventId, IClientSessionHandle session)
    {
        var filter = Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.Eq(doc => doc.ProfileId, new ObjectId(profileId)),
            Builders<ProfileEvent>.Filter.Eq(doc => doc.EventId, new ObjectId(eventId)),
            Builders<ProfileEvent>.Filter.Ne(doc => doc.Confirmed, false)
        );

        var confirmUpdate = Builders<ProfileEvent>.Update.Set(pe => pe.Confirmed, false);

        var result = await dbService.UpdateOneAsync(CollectionName.ProfileEvents, filter, confirmUpdate, session);
        return result.ModifiedCount > 0;
    }



    /*
        public async Task<ProfileEvent> ConfirmProfileEventById(string id)
        {
            var update = Builders<ProfileEvent>.Update
                .Set(doc => doc.Confirmed, true)
                .Set(doc => doc.UpdatedAt, DateTimeOffset.UtcNow);

            return  await dbService.PatchUpdateAsync(collectionName, id, update);
        }
    */



    /*
        public async Task SyncProfileEvents(EventUpdateQueueMessage message)
        {

            var eventProfiles = await eventProfileService.FindAllByEventId(message.EventId);

            if (eventProfiles == null || eventProfiles.Count == 0) return;

            var update = Builders<ProfileEvent>.Update
                .Set(doc => doc.EventUpdatedAt, DateTimeOffset.UtcNow)
                .Set(doc => doc.UpdatedAt, message.UpdatedAt);

            foreach (var eventProfile in eventProfiles)
            {
                // profileId helps sharding retrieving, order matters
                var filter = Builders<ProfileEvent>.Filter.And(
                    Builders<ProfileEvent>.Filter.Eq(doc => doc.ProfileId, eventProfile.ProfileId),
                    Builders<ProfileEvent>.Filter.Eq(doc => doc.Id, eventProfile.ProfileEventId)
                );

                var updatedProfileEvent = await dbService.FindOneAndUpdateAsync(
                    collectionName,
                    filter,
                    update
                );

                if (updatedProfileEvent == null)
                {
                    Console.WriteLine($"Warning: ProfileEvent with Id '{eventProfile.ProfileEventId}' and ProfileId '{eventProfile.ProfileId}' not found for update.");
                    throw new Exception();
                }
            }
        }
    

    private static FilterDefinition<ProfileEvent> GetRetrieveFromProfileFilter(string profileId, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        return Builders<ProfileEvent>.Filter.And(
                Builders<ProfileEvent>.Filter.Eq(doc => doc.ProfileId, new ObjectId(profileId)),
                Builders<ProfileEvent>.Filter.Gt(doc => doc.EventEndTime, startTime),
                Builders<ProfileEvent>.Filter.Lt(doc => doc.EventStartTime, endTime)
            );
    }

*/
}