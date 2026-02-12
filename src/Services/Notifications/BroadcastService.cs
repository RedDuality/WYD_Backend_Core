using Core.Model.Notifications;
using MongoDB.Bson;

namespace Core.Services.Notifications;


public class BroadcastService(
    NotificationService notificationService,
    ProfileIdResolverFactory resolverFactory)
{

    public async Task BroadcastUpdate(Notification notification, HashSet<ObjectId>? profileIds = null)
    {
        profileIds ??= await GetProfileIds(notification);

        if (profileIds.Count > 0)
            await notificationService.SendNotification(profileIds, notification.ToDictionary());
    }

    private async Task<HashSet<ObjectId>> GetProfileIds(Notification notification)
    {
        var profileFinder = resolverFactory.Resolve(notification.Type);
        return await profileFinder.GetNotificationProfileIdsAsync(notification);
    }

}
