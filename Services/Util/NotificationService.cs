/* using Google.Cloud.Firestore;
using Model;

namespace Service;

public enum UpdateType
{
    NewEvent,
    ShareEvent,
    UpdateEvent,
    UpdatePhotos,
    ConfirmEvent,
    DeclineEvent,
    DeleteEvent,
    DeleteForAll,
    UpdateProfile,
}

public class NotificationService
{
    private readonly FirestoreDb firestoreDb;

    public NotificationService()
    {
        var projectId = Environment.GetEnvironmentVariable("GoogleProjectId");
        var googleCredentialsJson = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS");

        if (string.IsNullOrEmpty(googleCredentialsJson) || string.IsNullOrEmpty(projectId))
        {
            throw new Exception("Google credentials not found in the environment variable");
        }

        firestoreDb = new FirestoreDbBuilder
        {
            ProjectId = projectId,
            JsonCredentials = googleCredentialsJson,
        }.Build();
    }

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
    }
}
 */