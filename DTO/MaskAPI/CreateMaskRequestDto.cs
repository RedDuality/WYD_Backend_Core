using Core.Model.Events;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Core.DTO.MaskAPI;

public class CreateMaskRequestDto
{
    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; set; }

    public string? Title { get; set; }

    public CreateMaskRequestDto() { }

}


