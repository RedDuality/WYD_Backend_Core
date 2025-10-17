using Core.Model.Notifications;
using Core.Model.QueueMessages;
using Core.Services.Notifications;
using MongoDB.Bson;

namespace Core.Services.Profiles;

// move this to Profileservice after having created the actual message service
public class ProfileUpdatePropagationService(
    BroadcastService broadcastService
//MessageQueueService messageService
)
{
    //currently not called by anyone
    public async Task PropagateUpdateEffects(ObjectId profileId, ProfileUpdateType type, string? actorId = null)
    {
        var notification = GetUpdateNotification(type, profileId, actorId);
        //await messageService.SendNotificationAsync(notification);
        _ = broadcastService.BroadcastUpdate(notification);
    }

    private static Notification GetUpdateNotification(ProfileUpdateType type, ObjectId profileId, string? actorId = null)
    {
        return type switch
        {
            ProfileUpdateType.update => new Notification(profileId, NotificationType.UpdateProfile),
            _ => new Notification(profileId, NotificationType.UpdateProfile),
        };
    }

}