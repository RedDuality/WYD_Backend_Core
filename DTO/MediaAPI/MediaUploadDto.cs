using Core.Model.Enum;

namespace Core.DTO.MediaAPI;

public record MediaInfo(string Id, DateTimeOffset? Date , string Mimetype);

public class MediaUploadDto
{
    public string ParentHash { get; set; } = "";
    public List<MediaInfo> Media { get; set; } = [];

    public MediaUploadDto() { }
}