namespace Core.Model.Dto.API;

public class RetrieveEventsDto
{
    public List<string>? ProfileHashes { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }

    public RetrieveEventsDto() { }
}