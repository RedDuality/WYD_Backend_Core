using Core.Model.Events;
using Core.Model.Notifications;
using Core.Model.QueueMessages;
using Core.Services.Notifications;
using Core.Services.Profiles;

namespace Core.Services.Events;

// move this to Eventservice after having created the actual message service
public class EventUpdatePropagationService(
    ProfileEventService profileEventService,
    EventProfileService eventProfileService,
    BroadcastService broadcastService
    //MessageQueueService messageService
)
{
    public async Task PropagateUpdateEffects(Event ev, EventUpdateType type, string? actorId = null)
    {
        var profileIds = await eventProfileService.GetProfileIdsAsync(ev.Id);
        if (profileIds.Count > 0)
        {
            await profileEventService.PropagateEventUpdatesAsync(ev, profileIds);

            var notification = GetUpdateNotification(type, ev, actorId);
            //await messageService.SendNotificationAsync(notification);
            _ = broadcastService.BroadcastUpdate(notification);

        }
    }

    private static Notification GetUpdateNotification(EventUpdateType type, Event ev, string? actorId = null)
    {
        return type switch
        {
            EventUpdateType.share => new Notification(ev.Id, NotificationType.UpdateEssentialsEvent, ev.UpdatedAt),
            EventUpdateType.update => new Notification(ev.Id, NotificationType.UpdateEssentialsEvent, ev.UpdatedAt),
            EventUpdateType.confirm => new Notification(ev.Id, NotificationType.ConfirmEvent, ev.UpdatedAt) { ActorId = actorId },
            EventUpdateType.decline => new Notification(ev.Id, NotificationType.DeclineEvent, ev.UpdatedAt) { ActorId = actorId },
            _ => new Notification(ev.Id, NotificationType.UpdateEssentialsEvent, ev.UpdatedAt),
        };
    }

}