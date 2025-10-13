using Core.Components.Database;
using Core.Model.Notifications;
using Core.Model.Profiles;
using Core.Model.Users;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Notifications;


public class BroadcastService(MongoDbService dbService, NotificationService notificationService, ProfileIdResolverFactory resolverFactory)
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
        return await GetProfilesNotificationTokens(affectedProfileIds);
    }

    private async Task<Dictionary<string, ObjectId>> GetProfilesNotificationTokens(List<ObjectId> profileIds)
    {
        var profileDetails = await dbService.RetrieveMultipleAsync(
                    CollectionName.ProfileDetails,
                    Builders<ProfileDetails>.Filter.In(p => p.ProfileId, profileIds)
                );
        var userIds = profileDetails.SelectMany(pd => pd.Users).Select(pu => pu.UserId).ToHashSet();

        var users = await dbService.RetrieveMultipleAsync(
            CollectionName.Users,
            Builders<User>.Filter.In(u => u.Id, userIds)
        );

        // Create the Token -> User ID Dictionary
        var tokensWithUserIds = new Dictionary<string, ObjectId>();

        foreach (var user in users)
        {
            foreach (var device in user.Devices)
            {
                if (!string.IsNullOrEmpty(device.FcmToken))
                {
                    // We use TryAdd to handle cases where multiple users might coincidentally
                    // share a token (or if a token is registered for multiple users in error,
                    // we pick the first User ID encountered).
                    tokensWithUserIds.TryAdd(device.FcmToken, user.Id);
                }
            }
        }

        return tokensWithUserIds;
    }

}
