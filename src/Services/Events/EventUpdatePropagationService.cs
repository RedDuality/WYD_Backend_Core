using Core.Model.Events;
using Core.Model.Notifications;
using Core.Model.QueueMessages;
using Core.Services.Masks;
using Core.Services.Notifications;
using Core.Services.Profiles;

namespace Core.Services.Events;

// move this to Eventservice after having created the actual message service
public class EventUpdatePropagationService(
    ProfileEventService profileEventService,
    EventProfileService eventProfileService,
    EventMaskService eventMaskService,
    BroadcastService broadcastService
)
{
    public async Task PropagateUpdateEffects(Event ev, EventUpdateType type, string? actorId = null)
    {
        var eventProfiles = await eventProfileService.FindAllByEventId(ev.Id);
        var profileIds = eventProfiles.Select(ep => ep.ProfileId).ToHashSet();

        if (profileIds.Count > 0)
        {
            var tasks = new List<Task>();

            // profileEvents
            if (type != EventUpdateType.create)
                tasks.Add(profileEventService.PropagateEventUpdatesAsync(ev, profileIds));

            // masks
            if (type != EventUpdateType.share)
                tasks.Add(eventMaskService.PropagateEventUpdateAsync(ev, type, profileIds, actorId));

            await Task.WhenAll(tasks);

            var notification = GetUpdateNotification(type, ev, actorId);
            _ = broadcastService.BroadcastUpdate(notification, profileIds);

        }
    }

    private static Notification GetUpdateNotification(EventUpdateType type, Event ev, string? actorId = null)
    {
        return type switch
        {
            EventUpdateType.create => new Notification(ev.Id, NotificationType.UpdateEssentialsEvent, ev.UpdatedAt) { ActorId = actorId },// To retrieve the mask
            EventUpdateType.share => new Notification(ev.Id, NotificationType.UpdateEssentialsEvent, ev.UpdatedAt),
            EventUpdateType.update => new Notification(ev.Id, NotificationType.UpdateEssentialsEvent, ev.UpdatedAt) { ActorId = actorId },// To retrieve the mask
            EventUpdateType.confirm => new Notification(ev.Id, NotificationType.ConfirmEvent, ev.UpdatedAt) { ActorId = actorId },
            EventUpdateType.decline => new Notification(ev.Id, NotificationType.DeclineEvent, ev.UpdatedAt) { ActorId = actorId },
            _ => new Notification(ev.Id, NotificationType.UpdateEssentialsEvent, ev.UpdatedAt),
        };
    }

}