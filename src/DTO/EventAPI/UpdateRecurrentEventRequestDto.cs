namespace Core.DTO.EventAPI;

public enum RecurrentUpdateType
{
    ThisInstance,
    AllTheSequence,
    ThisAndAllFollowing
}

public class UpdateRecurrentEventRequestDto
{
    public required RecurrentUpdateType UpdateType { get; set; }
    public required string InstanceId { get; set; } // recurrencyId if generated, eventId if detached
    public required string MasterEventId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? RecurrenceRule { get; set; }
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }

    // Parameterless constructor for deserialization
    public UpdateRecurrentEventRequestDto() { }

    public bool IsGeneratedInstance() {
        return InstanceId.Contains(MasterEventId);
    }

}
