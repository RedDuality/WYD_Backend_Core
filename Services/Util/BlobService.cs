/* using System.Collections.Concurrent;
using Azure.Storage.Blobs;
using Dto;
using Model;

namespace Service;

public class BlobService
{
    private static readonly BlobServiceClient blobServiceClient = new(
        Environment.GetEnvironmentVariable("BlobConnectionString")
            ?? throw new Exception("BlobConnectionString not found")
    );

    private static readonly ConcurrentDictionary<string, BlobContainerClient> containerCache =
        new();

    private static readonly Dictionary<string, string> MimeTypeToExtension = new()
    {
        { "image/jpeg", ".jpg" },
        { "image/png", ".png" },
        { "image/gif", ".gif" },
        { "image/bmp", ".bmp" },
        { "image/tiff", ".tiff" },
        /*{ "image/webp", ".webp" },
        { "video/mp4", ".mp4" },
        { "video/x-msvideo", ".avi" },
        { "video/quicktime", ".mov" },
        { "video/x-ms-wmv", ".wmv" },
        { "video/x-flv", ".flv" },
        { "video/x-matroska", ".mkv" }*//*
    };

    private static string SanifyBlobData(BlobData blobData)
    {
        if (blobData.Data == null || blobData.Data.Length == 0)
        {
            throw new ArgumentException("Data cannot be null or empty.");
        }

        const int maxSizeInBytes = 20 * 1024 * 1024; // 20MB in bytes
        if (blobData.Data.Length > maxSizeInBytes)
        {
            throw new ArgumentException("Data cannot be larger than 20MB.");
        }
        if (
            string.IsNullOrWhiteSpace(blobData.MimeType)
            || !MimeTypeToExtension.ContainsKey(blobData.MimeType.ToLower())
        )
        {
            throw new ArgumentException(
                $"MIME type '{blobData.MimeType}' is not a valid Blob/video format."
            );
        }

        return MimeTypeToExtension[blobData.MimeType.ToLower()];
    }

    public static async Task<BlobContainerClient> GetContainerClientAsync(string parentHash)
    {
        string containerName = parentHash.ToLower();

        if (!containerCache.TryGetValue(containerName, out var containerClient))
        {
            containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(
                publicAccessType: Azure.Storage.Blobs.Models.PublicAccessType.Blob
            );

            containerCache.TryAdd(containerName, containerClient);
        }

        return containerClient;
    }

    public static string UploadBlob(
        BlobContainerClient containerClient,
        Blob blob,
        BlobData blobData
    )
    {
        var extension = SanifyBlobData(blobData);

        string blobName = blob.Hash.ToLower() + extension; // Use Blob hash as blob name
        var blobClient = containerClient.GetBlobClient(blobName);

        //blob is always considered uploaded
        blobClient
            .UploadAsync(new MemoryStream(blobData.Data), overwrite: true)
            .ContinueWith(
                t =>
                {
                    if (t.IsFaulted)
                    {
                        //TODO Log the exception or handle it as needed
                        Console.WriteLine(t.Exception?.Message);
                    }
                },
                TaskContinuationOptions.OnlyOnFaulted
            );
        return extension;
    }
}
 */