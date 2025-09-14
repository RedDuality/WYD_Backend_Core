
using Core.Model.Join;
using Core.Components.Database;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Model;

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

    public async Task<ProfileEvent> RetrieveProfileEventById(string id)
    {
        return await dbService.RetrieveByIdAsync<ProfileEvent>(profileEventCollection, id);
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


    public async Task<List<ProfileEvent>> FindByProfileId(string profileId, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var filter = GetRetrieveFromProfileFilter(profileId, startTime, endTime);
        return await dbService.FindAsync(profileEventCollection, filter);
    }
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
            var filter = Builders<ProfileEvent>.Filter.And(
                Builders<ProfileEvent>.Filter.Eq(doc => doc.Id, eventProfile.ProfileEventId),
                Builders<ProfileEvent>.Filter.Eq(doc => doc.ProfileId, eventProfile.ProfileId)
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
*/

    private static FilterDefinition<ProfileEvent> GetRetrieveFromProfileFilter(string profileId, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        return Builders<ProfileEvent>.Filter.And(
                Builders<ProfileEvent>.Filter.Eq(doc => doc.ProfileId, new ObjectId(profileId)),
                Builders<ProfileEvent>.Filter.Gt(doc => doc.EventEndTime, startTime),
                Builders<ProfileEvent>.Filter.Lt(doc => doc.EventStartTime, endTime)
            );
    }


}