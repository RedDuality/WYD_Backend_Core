using MongoDB.Bson;

namespace Core.Model.Notifications;

public class Notification(
    ObjectId objectId,
    NotificationType type,
    DateTimeOffset? updatedAt = null)
{
    public ObjectId ObjectId { get; set; } = objectId;
    public NotificationType Type { get; set; } = type;
    public DateTimeOffset? UpdatedAt { get; set; } = updatedAt;
    
    public string? Title { get; set; } = null;
    public string? Body { get; set; } = null;
    public string? ActorId { get; set; } = null;

    public Dictionary<string, string> ToDictionary()
    {
        Dictionary<string, string> data = new() {
                { "type", Type.ToString() },
                { "id", ObjectId.ToString() }
            };

        if (UpdatedAt != null) data.Add("time",UpdatedAt.Value.ToString("o")); // "o" = ISO 8601 format

        if (Title != null) data.Add("title", Title);

        if (Body != null) data.Add("body", Body);

        if (ActorId != null) data.Add("profileId", ActorId);

        return data;
    }
}
