using Core.Components.ObjectStorage;
using Core.Model;
using Core.Model.Enum;
using MongoDB.Bson;

namespace Core.DTO.MediaAPI;

public class MediaReadResponseDto
{
    public string Id { get; set; }

    //public string? ParentId { get; set; }

    public string? Extension { get; set; }
    public string? Name { get; set; }
    public DateTimeOffset? CreationDate { get; set; }

    //public string? Type { get; set; }

    public MediaVisibility? Visibility { get; set; }
    public string? Url { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? Error { get; set; }

    public MediaReadResponseDto(Media media, string url, DateTime validUntil)
    {
        Id = media.Id.ToString();
        //ParentId = media.ParentId.ToString();
        Extension = media.Extension;
        Name = media.Name;
        CreationDate = media.CreationDate;
        //Type = bucketName.ToString();
        Visibility = media.Visibility;
        Url = url;
        ValidUntil = validUntil;
    }

    public MediaReadResponseDto(ObjectId id, string error)
    {
        Id = id.ToString();
        Error = error;
    }

}