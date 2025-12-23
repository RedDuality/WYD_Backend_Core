
using Core.Model.Masks;
using MongoDB.Bson;

namespace Core.DTO.MaskAPI;

public class RetrieveMaskResponseDto(Mask mask)
{
    public string Id {get; set; } = mask.Id.ToString(); 
    //public string ProfileId = mask.ProfileId.ToString();
    public DateTimeOffset StartTime {get; set; } =  mask.StartTime;
    public DateTimeOffset EndTime {get; set; } = mask.EndTime;
    public string? Title {get; set; } = mask.Title;
    public ObjectId? EventId {get; set;} = mask.EventId;
}

