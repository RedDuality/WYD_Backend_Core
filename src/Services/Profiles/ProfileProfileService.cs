using Core.Model.Notifications;
using Core.Services.Notifications;
using MongoDB.Bson;

namespace Core.Services.Profiles;

public class ProfileProfileService() : INotificationProfileFinder
{
    // for notifications, to avoid circular injection
    public async Task<HashSet<ObjectId>> GetNotificationProfileIdsAsync(Notification notification)
    {
        return await Task.FromResult(new HashSet<ObjectId> { notification.ObjectId });
    }
}
