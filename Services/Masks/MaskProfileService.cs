
using Core.Model.Notifications;
using Core.Services.Notifications;
using MongoDB.Bson;

namespace Core.Services.Masks;

public class MaskProfileService(
) : INotificationProfileFinder
{
    // for notifications, to avoid circular injection
    public async Task<HashSet<ObjectId>> GetNotificationProfileIdsAsync(Notification notification)
    {
        var profileId = new ObjectId(notification.ActorId); // non-event masks are related to only one profile
        return [profileId];
    }

}