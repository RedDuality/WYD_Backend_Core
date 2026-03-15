
namespace Core.DTO.EventAPI;

public record RetrieveRecurrenceInstanceDetailsRequestDto
{
    required public string MasterEventId { get; set; }
    required public string RecurrencyInstanceId { get; set; }
    public RetrieveRecurrenceInstanceDetailsRequestDto() { }

}
