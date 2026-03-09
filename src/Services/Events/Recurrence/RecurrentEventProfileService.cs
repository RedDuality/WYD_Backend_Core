using MongoDB.Driver;
using MongoDB.Bson;
using Core.Components.Database;
using Core.Model.Profiles;
using Core.Model.Events;
using Core.Services.Notifications;
using Core.Model.Notifications;
using Core.Model.Events.Recurrence;

namespace Core.Services.Events.Recurrence;

public class RecurrentEventProfileService(MongoDbService dbService) : INotificationProfileFinder
{
    private readonly CollectionName recurrentEventProfileCollection = CollectionName.RecurrentEventProfiles;

    public async Task<RecurrentEventProfile> CreateEventProfileAsync(ProfileRecurrentEvent profileEvent, IClientSessionHandle? session)
    {
        var eventProfile = new RecurrentEventProfile(profileEvent);
        await dbService.CreateOneAsync(recurrentEventProfileCollection, eventProfile, session);
        return eventProfile;
    }

    public async Task<List<EventProfile>> FindAllByEventId(ObjectId eventId)
    {
        var result = await dbService.RetrieveMultipleAsync(
            recurrentEventProfileCollection,
            Builders<EventProfile>.Filter.Eq(ep => ep.EventId, eventId));

        var eventProfiles = result?.ToList();
        if (eventProfiles == null || eventProfiles.Count == 0)
        {
            throw new InvalidOperationException($"No event profiles found for EventId: {eventId}");
        }

        return eventProfiles;
    }

    public async Task<HashSet<ObjectId>> GetNotificationProfileIdsAsync(Notification notification)
    {
        var eps = await FindAllByEventId(notification.ObjectId);
        return [.. eps.Select(ep => ep.ProfileId)];
    }

}
