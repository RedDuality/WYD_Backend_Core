using Core.Model;
using MongoDB.Bson;

namespace Core.DTO.MediaAPI;

public class MediaReadUrlDto
{
    public string Id { get; set; }
    public string? ParentId { get; set; }
    public string? Extension { get; set; }
    public string? Url { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? Error { get; set; }

    public MediaReadUrlDto(Media media, string url, DateTime validUntil)
    {
        Id = media.Id.ToString();
        ParentId = media.ParentId.ToString();
        Extension = media.Extension;
        Url = url;
        ValidUntil = validUntil;
    }

    public MediaReadUrlDto(ObjectId id, string error)
    {
        Id = id.ToString();
        Error = error;
    }
}
