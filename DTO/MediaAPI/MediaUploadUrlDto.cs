using Core.Components.ObjectStorage;
using Core.Model;
using Core.Model.Enum;

namespace Core.DTO.MediaAPI;

public class MediaUploadUrlDto
{
    public string TempId { get; set; }
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Extension { get; set; }
    public string? Type { get; set; }
    public MediaVisibility? Visibility { get; set; }
    public string? Url { get; set; }

    public string? Error { get; set; }

    public MediaUploadUrlDto(string tempId, Media media, string url, BucketName bucketName)
    {
        TempId = tempId;
        Id = media.Id.ToString();
        Name = media.Name;
        Extension = media.Extension;
        Type = bucketName.ToString();
        Visibility = media.Visibility;
        Url = url;
    }

    public MediaUploadUrlDto(string tempId)
    {
        TempId = tempId;
    }
}
