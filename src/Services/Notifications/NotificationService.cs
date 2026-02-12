using Core.Components.ServerSentMessages;
using Core.External.FCM;
using Core.Services.Profiles;
using MongoDB.Bson;

namespace Core.Services.Notifications;

public class NotificationService(ISseService sseService, FCMService fcmService, ProfileDetailsService profileDetailsService)
{

    public async Task SendNotification(HashSet<ObjectId> profileIds, Dictionary<string, string>? data = null)
    {
        var users = await profileDetailsService.RetrieveByProfileIds(profileIds);
        var userIds = users.Select((u) => u.Id.ToString()).ToHashSet();

        string message = data != null
        ? System.Text.Json.JsonSerializer.Serialize(data)
        : string.Empty;

        sseService.SendToUsers([.. userIds.Select((id) => id.ToString())], message);

        await fcmService.Send(users, data);
    }


}
