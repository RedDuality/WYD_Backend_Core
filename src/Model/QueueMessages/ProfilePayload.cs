using MongoDB.Bson;

namespace Core.Model.QueueMessages;

public enum ProfileUpdateType
{
    update,
}

public class UpdateProfilePayload(ObjectId profileId, ProfileUpdateType type, string? actorId = null)
{
    public ObjectId ProfileId { get; set; } = profileId;

    public ProfileUpdateType Type { get; set; } = type;

    public string? ActorId { get; set; } = actorId;
}
