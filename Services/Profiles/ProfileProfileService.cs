using Core.Services.Notifications;
using MongoDB.Bson;

namespace Core.Services.Profiles;

public class ProfileProfileService() : IProfileFinder
{
    // for notifications, to avoid circular injection
    public async Task<List<ObjectId>> GetProfileIdsAsync(ObjectId profileId)
    {
        return await Task.FromResult(new List<ObjectId> { profileId });
    }
}
