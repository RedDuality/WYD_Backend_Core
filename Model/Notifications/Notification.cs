namespace Core.Model.Notifications;

public class Notification(string objectId, NotificationType type)
{
    public string ObjectId { get; set; } = objectId;
    public NotificationType Type { get; set; } = type;

    public string? Title { get; set; } = null;
    public string? Body { get; set; } = null;
    public string? ProfileId { get; set; } = null;

    public Dictionary<string, string> ToDictionary()
    {
        Dictionary<string, string> data = new() {
                { "type", type.ToString() },
                { "hash", ObjectId }
            };

        if (Title != null)
            data.Add("title", Title);

        if (Body != null)
            data.Add("body", Body);

        if (ProfileId != null)
            data.Add("profileHash", ProfileId);

        return data;
    }
}
