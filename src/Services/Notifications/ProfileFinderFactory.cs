using Core.Model.Notifications;
using Core.Services.Communities;
using Core.Services.Events.Instances;
using Core.Services.Masks;
using Core.Services.Profiles;
using MongoDB.Bson;

namespace Core.Services.Notifications;

public interface INotificationProfileFinder
{
    Task<HashSet<ObjectId>> GetNotificationProfileIdsAsync(Notification notification);
}

public class ProfileIdResolverFactory(
    EventProfileService eventProfileService,
    CommunityProfileService communityProfileService,
    ProfileProfileService profileService,
    MaskProfileService maskProfileService)
{

#pragma warning disable CS8524
    public INotificationProfileFinder Resolve(NotificationType type) =>
        type switch
        {
            NotificationType.UpdateEssentialsEvent => eventProfileService,
            NotificationType.UpdatePhotos => eventProfileService,
            NotificationType.ConfirmEvent => eventProfileService,
            NotificationType.DeclineEvent => eventProfileService,
            NotificationType.DeleteEvent => eventProfileService,
            NotificationType.DeleteEventForAll => eventProfileService,

            NotificationType.UpdateProfile => profileService,

            NotificationType.CreateCommunity => communityProfileService,

            NotificationType.UpdateMask => maskProfileService,
            NotificationType.DeleteMask => maskProfileService,
        };
#pragma warning restore CS8524

}
