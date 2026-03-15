using Core.Model.Events.Recurrence;
using Core.Model.Notifications;
using Core.Model.QueueMessages;
using Core.Services.Masks;
using Core.Services.Notifications;
using Core.Services.Profiles;

namespace Core.Services.Events.Recurrence;

public class RecurrentEventUpdatePropagationService(
    ProfileRecurrentEventService profileREventService,
    RecurrentEventProfileService rEventProfileService,
    EventMaskService eventMaskService,
    BroadcastService broadcastService
)
{
    public async Task PropagateUpdateEffects(RecurrentEvent rev, EventUpdateType type, string? actorId = null)
    {
        var eventProfiles = await rEventProfileService.FindAllByEventId(rev.Id);
        var profileIds = eventProfiles.Select(ep => ep.ProfileId).ToHashSet();

        if (profileIds.Count > 0)
        {
            var tasks = new List<Task>();

            // update profileRecurrentEvents
            if (type != EventUpdateType.create)
                tasks.Add(profileREventService.PropagateEventUpdatesAsync(rev, profileIds));

            // update/create masks
            if (type != EventUpdateType.share)
                tasks.Add(eventMaskService.PropagateRecurrentEventUpdateAsync(rev, type, profileIds, actorId));

            await Task.WhenAll(tasks);

            var notification = GetUpdateNotification(type, rev, actorId);
            _ = broadcastService.BroadcastUpdate(notification, profileIds);

        }
    }

    private static Notification GetUpdateNotification(EventUpdateType type, RecurrentEvent ev, string? actorId = null)
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