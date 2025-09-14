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


    public async Task<List<MediaUploadUrlDto>> GetUploadUrlsAsync(Profile profile, BucketName bucketName, CollectionName mediaCollection, MediaUploadDto dto)
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


                return new MediaUploadUrlDto(media.Id, createdMedia, url, bucketName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate URL for extension '{media.Mimetype}': {ex.Message}");
                return new MediaUploadUrlDto(media.Id) { Error = ex.Message };
            }
        });
        var dtos = await Task.WhenAll(tasks);
        return dtos.ToList();
    }

    private async Task<Media> CreateMedia(CollectionName mediaCollection, Media newMedia)
    {
        return await dbService.CreateOneAsync(mediaCollection, newMedia, nul<l)>;
    }

    public async Task<List<MediaReadUrlDto>> GetReadUrlsAsync(BucketName bucketName, List<string> parentIds)
    {
        var objectIds = parentIds.Select(id => new ObjectId(id)).ToList();

        var filter = Builders<Media>.Filter.In(m => m.ParentId, objectIds);

        var media = await dbService.FindAsync(CollectionName.EventMedia, filter);

        var tasks = media.Select(async m =>
        {
            var name = m.ParentId.ToString() + '/' + m.Id.ToString() + m.Extension;
            try
            {
                var validUntil = DateTime.UtcNow.AddSeconds(600); // URL valid for 10 mins
                var url = await minioClient.GetReadUrl(bucketName, name, validUntil);
                return new MediaReadUrlDto(m, url, validUntil);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve URL for image '{m.Id}': {ex.Message}");
                return new MediaReadUrlDto(m.Id, ex.Message);
            }
        });
        var dtos = await Task.WhenAll(tasks);
        return dtos.ToList();
    }



}
