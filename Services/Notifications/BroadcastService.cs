using Core.Components.Database;
using Core.Model.Events;
using Core.Model.Notifications;
using Core.Model.Profiles;
using Core.Model.Users;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Notifications;


public class BroadcastService(MongoDbService dbService, NotificationService notificationService)
{
    public async Task BroadcastEventUpdate(string eventId, NotificationType type, string? title = null, string? body = null, string? profileId = null)
    {
        var tokens = await GetEventNotificationTokens(eventId);
        if (tokens.Count > 0)
        {
            Dictionary<string, string> data = new() {
                { "type", type.ToString() },
                { "hash", eventId }
            };

            if (title != null)
                data.Add("title", title);

            if (body != null)
                data.Add("body", body);

            if (profileId != null)
                data.Add("profileHash", profileId);

            await notificationService.SendNotification(tokens, data);
        }
    }

    private async Task<List<string>> GetEventNotificationTokens(string eventId)
    {
        var eventProfiles = await dbService.RetrieveMultipleAsync(
            CollectionName.EventProfiles,
            Builders<EventProfile>.Filter.Where(ep => ep.EventId == new ObjectId(eventId))
        );
        var profileIds = eventProfiles.Select(ep => ep.ProfileId).ToList();

        return await GetProfilesNotificationTokens(profileIds);
    }

    private async Task<List<string>> GetProfilesNotificationTokens(List<ObjectId> profileIds)
    {
        var profileDetails = await dbService.RetrieveMultipleAsync(
                    CollectionName.ProfileDetails,
                    Builders<ProfileDetails>.Filter.In(p => p.ProfileId, profileIds)
                );
        var userIds = profileDetails.SelectMany(pd => pd.Users).Select(pu => pu.UserId).ToHashSet();

        var users = await dbService.RetrieveMultipleAsync(
            CollectionName.Users,
            Builders<User>.Filter.In(u => u.Id, userIds)
        );
        var fcmTokens = users.SelectMany(u => u.Devices).Select(d => d.FcmToken).ToHashSet().ToList();

        return fcmTokens;
    }

}
