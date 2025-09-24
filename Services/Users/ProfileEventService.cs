
using Core.Model.Join;
using Core.Components.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using Core.Services.Events;
using Core.Model.Profile;


namespace Core.Services.Users;

public class ProfileEventService(MongoDbService dbService, EventProfileService eventProfileService)
{
    private readonly CollectionName profileEventCollection = CollectionName.ProfileEvents;

    public async Task<ProfileEvent> CreateProfileEventAsync(ProfileEvent profileEvent, IClientSessionHandle session)
    {
        profileEvent = await dbService.CreateOneAsync(profileEventCollection, profileEvent, session);
        var eventProfile = new EventProfile(profileEvent);
        await eventProfileService.CreateEventProfileAsync(eventProfile, session);
        return profileEvent;
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