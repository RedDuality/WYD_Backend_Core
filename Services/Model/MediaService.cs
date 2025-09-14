using System.Management;
using Core.Components.Database;
using Core.Components.ObjectStorage;
using Core.DTO.MediaAPI;
using Core.Model;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Services.Model;

public class MediaService(MongoDbService dbService, MinioClient minioClient)
{
    private static readonly Dictionary<string, string> MimeTypeToExtension = new()
    {
        { "image/jpeg", ".jpg" },
        { "image/png", ".png" },
        { "image/gif", ".gif" },
        { "video/mp4", ".mp4" },
        { "application/pdf", ".pdf" }
    };

    public static string SanifyMimeType(string mimeType)
    {
        var mt = mimeType.ToLower();
        if (
            string.IsNullOrWhiteSpace(mt)
            || !MimeTypeToExtension.TryGetValue(mt, out string? value)
        )
        {
            throw new ArgumentException(
                $"MIME type '{mt}' is not a valid Blob/video format."
            );
        }

        return value;
    }

    private static string CreateMediaName(Profile profile, DateTimeOffset creationDate)
    {
        string timestamp = creationDate.ToString("yyyyMMdd_HHmmss");
        return $"{timestamp}_{profile.Tag}";
    }


    public async Task<List<MediaUploadResponseDto>> GetUploadUrlsAsync(Profile profile, BucketName bucketName, CollectionName mediaCollection, MediaUploadRequestDto dto)
    {
        var tasks = dto.Media.Select(async media =>
        {
            try
            {
                var ext = SanifyMimeType(media.Mimetype);
                DateTimeOffset date = media.Date ?? DateTimeOffset.UtcNow;
                string name = CreateMediaName(profile, date);


                var createdMedia = await CreateMedia(
                    mediaCollection,
                    new Media
                    {
                        ParentId = new ObjectId(dto.ParentHash),
                        OwnerId = profile.Id,
                        Extension = ext,
                        Name = name,
                        CreationDate = date,
                    }
                );

                var path = $"{dto.ParentHash}/{createdMedia.Id}{createdMedia.Extension}";
                var validUntil = DateTime.UtcNow.AddSeconds(600); // URL valid for 10 mins

                var url = await minioClient.GetUploadUrl(bucketName, path, validUntil);


                return new MediaUploadResponseDto(media.Id, createdMedia, url, bucketName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate URL for extension '{media.Mimetype}': {ex.Message}");
                return new MediaUploadResponseDto(media.Id) { Error = ex.Message };
            }
        });
        var dtos = await Task.WhenAll(tasks);
        return dtos.ToList();
    }

    private async Task<Media> CreateMedia(CollectionName mediaCollection, Media newMedia)
    {
        return await dbService.CreateOneAsync(mediaCollection, newMedia, null);
    }

    public async Task<List<MediaReadResponseDto>> GetReadUrlsAsync(BucketName bucketName, MediaReadRequestDto mediaReadRequestDto)
    {
        var parentId = new ObjectId(mediaReadRequestDto.ParentHash);

        var sortDefinition = Builders<Media>.Sort.Descending("creationDate");
        var filter = Builders<Media>.Filter.Eq(m => m.ParentId, parentId);

        var media = await dbService.FindForPaginationAsync(
            CollectionName.EventMedia,
            filter,
            sortDefinition,
            mediaReadRequestDto.PageNumber,
            mediaReadRequestDto.PageSize
        );

        var tasks = media.Select(async m =>
        {
            var name = m.ParentId.ToString() + '/' + m.Id.ToString() + m.Extension;
            try
            {
                var validUntil = DateTime.UtcNow.AddDays(1); // URL valid for 1 day
                var url = await minioClient.GetReadUrl(bucketName, name, validUntil);
                return new MediaReadResponseDto(m, url, validUntil);
            }
            catch (Exception ex)
            {
                var errorMessagge = $"Failed to retrieve URL";
                Console.WriteLine($"{errorMessagge} for image '{m.Id}': {ex.Message}");
                return new MediaReadResponseDto(m.Id, ex.Message);
            }
        });
        var dtos = await Task.WhenAll(tasks);
        return dtos.ToList();
    }



}
