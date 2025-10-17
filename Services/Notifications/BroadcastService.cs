using Core.Model.Notifications;
using Core.Services.Users;
using MongoDB.Bson;

namespace Core.Services.Notifications;


public class BroadcastService(
    NotificationService notificationService,
    ProfileIdResolverFactory resolverFactory,
    DeviceService deviceService)
{

    public async Task BroadcastUpdate(Notification notification)
    {
        var tokens = await GetNotificationTokens(notification);
        if (tokens.Count > 0)
            await notificationService.SendNotification(tokens, notification.ToDictionary());
    }

    public async Task<Dictionary<string, ObjectId>> GetNotificationTokens(Notification notification)
    {
        var profileFinder = resolverFactory.Resolve(notification.Type);
        var affectedProfileIds = await profileFinder.GetProfileIdsAsync(notification.ObjectId);
        return await deviceService.GetProfilesDevicesTokens(affectedProfileIds);
    }

}
