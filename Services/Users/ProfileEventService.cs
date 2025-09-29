using Core.Components.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using Core.Services.Events;
using Core.Model.Profiles;
using Core.Model.Events;


namespace Core.Services.Users;

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
        await eventProfileService.CreateMultipleEventProfileAsync(profileEvents,session);

        return profileEvents;
    }




    public async Task<ProfileEvent> FindByProfileAndEventId(string profileId, string eventId)
    {
        var filter = Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.Eq(doc => doc.ProfileId, new ObjectId(profileId)),
            Builders<ProfileEvent>.Filter.Eq(doc => doc.EventId, new ObjectId(eventId))
        );
        return await dbService.RetrieveAsync(profileEventCollection, filter);
    }

    public async Task Confirm(string profileId, string eventId, IClientSessionHandle session)
    {
        var filter = Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.Eq(doc => doc.ProfileId, new ObjectId(profileId)),
            Builders<ProfileEvent>.Filter.Eq(doc => doc.EventId, new ObjectId(eventId))
        );
        var confirmUpdate = Builders<ProfileEvent>.Update.Set(pe => pe.Confirmed, true);
        await dbService.UpdateOneAsync(CollectionName.ProfileEvents, filter, confirmUpdate, session);
    }

    public async Task Decline(string profileId, string eventId, IClientSessionHandle session)
    {
        var filter = Builders<ProfileEvent>.Filter.And(
            Builders<ProfileEvent>.Filter.Eq(doc => doc.ProfileId, new ObjectId(profileId)),
            Builders<ProfileEvent>.Filter.Eq(doc => doc.EventId, new ObjectId(eventId))
        );
        var confirmUpdate = Builders<ProfileEvent>.Update.Set(pe => pe.Confirmed, false);
        await dbService.UpdateOneAsync(CollectionName.ProfileEvents, filter, confirmUpdate, session);
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