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
    private readonly ProfileIdResolverFactory _resolverFactory = new();

    public async Task BroadcastUpdate(Notification notification)
    {
        var tokens = await GetNotificationTokens(notification);
        if (tokens.Count > 0)
            await notificationService.SendNotification(tokens, notification.ToDictionary());
    }

    public async Task<List<string>> GetNotificationTokens(Notification notification)
    {
        var profileFinder = _resolverFactory.Resolve(notification.Type);
        var affectedProfileIds = await profileFinder.GetProfileIdsAsync(dbService, notification);
        return await GetProfilesNotificationTokens(affectedProfileIds);
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
