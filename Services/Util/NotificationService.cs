using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;

namespace Core.Services.Util;

public class NotificationService
{

    private readonly Lazy<FirebaseMessaging> _messagingInstance;

    public NotificationService(IConfiguration configuration)
    {

        _messagingInstance = new Lazy<FirebaseMessaging>(() =>
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                var googleCredentials = configuration["GOOGLE_APPLICATION_CREDENTIALS"]
                    ?? throw new InvalidOperationException("'GOOGLE_APPLICATION_CREDENTIALS' is not set in configuration.");

                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromJson(googleCredentials)
                });
            }

            return FirebaseMessaging.DefaultInstance;
        }, isThreadSafe: true);
    }

    private FirebaseMessaging Messaging => _messagingInstance.Value;

    public async Task SendNotification(List<string> tokens, Dictionary<string, string>? data = null)
    {
        var message = new MulticastMessage()
        {
            Notification = null,
            Tokens = tokens,
            Data = data,
        };

        // Send the message
        BatchResponse response = await Messaging.SendEachForMulticastAsync(message);
        var failed = response.Responses.Where(r => r.IsSuccess == false).ToList();
        //TODO failed.forEach( check error message -> delete token);
    }

    /*
    https://firebase.flutter.dev/docs/messaging/server-integration/
    Data-only messages are sent as low priority on both Android and iOS and will not trigger the background handler by default. 
    To enable this functionality, you must set the "priority" to high on Android and enable the content-available flag for iOS in the message payload.
    */
    /*
    public async Task SendEventNotifications(Event ev, Profile currentProfile, UpdateType type)
    {
        //TODO add control over roles
        var eventUserHashes = ev
            .Profiles.SelectMany(profile => profile.Users.Select(user => user.Hash))
            .ToHashSet();

        await Send(
            eventUserHashes,
            type,
            ev.Hash,
            (type == UpdateType.ConfirmEvent || type == UpdateType.DeclineEvent)
                ? currentProfile
                : null
        );
    }

    public async Task SendEventNotifications(
        Event? ev,
        Profile currentProfile,
        UpdateType type,
        string hash
    )
    {
        //TODO add control over roles
        HashSet<Profile> profiles = [currentProfile];
        if (ev != null)
            profiles.UnionWith(ev.Profiles);

        var eventUserHashes = profiles
            .SelectMany(profile => profile.Users.Select(user => user.Hash))
            .ToHashSet();

        await Send(
            eventUserHashes,
            type,
            ev?.Hash ?? hash,
            (
                type == UpdateType.ConfirmEvent
                || type == UpdateType.DeclineEvent
                || type == UpdateType.DeleteEvent
            )
                ? currentProfile
                : null
        );
    }

    public async Task Send(
        IEnumerable<string> userHashes,
        UpdateType type,
        string objectHash,
        Profile? profile
    )
    {
        Dictionary<string, object> update = new()
        {
            { "timestamp", Timestamp.GetCurrentTimestamp() },
            { "type", type },
            { "hash", objectHash },
            { "v", "1.0" },
        };

        if (profile != null)
        {
            update.Add("phash", profile.Hash);
        }

        foreach (string hash in userHashes)
        {
            await firestoreDb.Collection(hash).AddAsync(update);
        }
    }

    public async Task SendMockNotification(string hash)
    {
        await Send([hash], UpdateType.UpdateEvent, "prova", null);
    }*/
}
