using Core.Components.ObjectStorage;
using Core.Model.MediaStorage;

namespace Core.DTO.MediaAPI;

public class MediaUploadResponseDto
{
    public string TempId { get; set; }
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Extension { get; set; }
    public string? Type { get; set; }
    public MediaVisibility? Visibility { get; set; }
    public string? Url { get; set; }

    public string? Error { get; set; }

    public MediaUploadResponseDto(string tempId, Media media, string url, BucketName bucketName)
    {
        TempId = tempId;
        Id = media.Id.ToString();
        Name = media.Name;
        Extension = media.Extension;
        Type = bucketName.ToString();
        Visibility = media.Visibility;
        Url = url;
    }

    public MediaUploadResponseDto(string tempId)
    {
        TempId = tempId;
    }
}
