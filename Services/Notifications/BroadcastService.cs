using Core.Model.Notifications;
using MongoDB.Bson;

namespace Core.Services.Notifications;


public class BroadcastService(
    NotificationService notificationService,
    ProfileIdResolverFactory resolverFactory)
{

    public async Task BroadcastUpdate(Notification notification)
    {
        var profileIds = await GetProfileIds(notification);
        if (profileIds.Count > 0)
            await notificationService.SendNotification(profileIds, notification.ToDictionary());
    }

    public async Task<List<ObjectId>> GetProfileIds(Notification notification)
    {
        var profileFinder = resolverFactory.Resolve(notification.Type);
        return await profileFinder.GetProfileIdsAsync(notification.ObjectId);
    }

}
