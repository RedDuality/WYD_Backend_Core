using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace Core.Components.ObjectStorage;

public class MinioClient
{
    private readonly HashSet<string> checkedBuckets = [];
    private readonly AmazonS3Client internalS3Client;
    private readonly AmazonS3Client publicS3Client;
    private readonly bool IsLocalDevelopment;

    public MinioClient(IConfiguration configuration)
    {
        IsLocalDevelopment = configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Local";
        string minioEndpoint = configuration.GetValue<string>("OBJ_STORAGE_ENDPOINT")
            ?? throw new Exception("Object Storage connection failed: 'OBJ_STORAGE_ENDPOINT' is not set in configuration.");
        string minioAppUser = configuration.GetValue<string>("OBJ_STORAGE_USER")
            ?? throw new Exception("Object Storage connection failed: 'OBJ_STORAGE_USER' is not set in configuration.");
        string minioAppPassword = configuration.GetValue<string>("OBJ_STORAGE_PASSWORD")
            ?? throw new Exception("Object Storage connection failed: 'OBJ_STORAGE_PASSWORD' is not set in configuration.");
        string publicEndpoint = configuration.GetValue<string>("OBJ_STORAGE_PUBLIC_ENDPOINT")
            ?? throw new Exception("Object Storage connection failed: 'OBJ_STORAGE_PUBLIC_ENDPOINT' is not set in configuration.");

        // Internal client (talks directly to MinIO service inside cluster)
        var internalConfig = new AmazonS3Config
        {
            ServiceURL = minioEndpoint,
            ForcePathStyle = true
        };
        internalS3Client = new AmazonS3Client(minioAppUser, minioAppPassword, internalConfig);

        // Public client (signs URLs exactly as the browser will call them)
        var publicConfig = new AmazonS3Config
        {
            ServiceURL = publicEndpoint,
            ForcePathStyle = true
        };
        publicS3Client = new AmazonS3Client(minioAppUser, minioAppPassword, publicConfig);
    }

    public async Task TestConnection()
    {
        var response = await internalS3Client.ListBucketsAsync();
        Console.WriteLine("Successfully connected to MinIO service. Buckets: " +
                          string.Join(", ", response.Buckets.Select(b => b.BucketName)));
    }

    private async Task CreateBucketIfNotExistAsync(BucketName bn)
    {
        string bucketName = bn.ToString();
        if (checkedBuckets.Contains(bucketName)) return;

        try
        {
            var listBucketsResponse = await internalS3Client.ListBucketsAsync();
            bool bucketExists = listBucketsResponse?.Buckets?.Any(b => b.BucketName == bucketName) == true;

            if (!bucketExists)
            {
                await internalS3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = bucketName
                });
                Console.WriteLine($"Bucket '{bucketName}' created successfully.");
            }
            else
            {
                Console.WriteLine($"Bucket '{bucketName}' already exists.");
            }

            checkedBuckets.Add(bucketName);
        }
        catch (Exception ex)
        {
            throw new Exception($"There was an error while trying to create the bucket '{bucketName}'", ex);
        }
    }

    public async Task<string> GetUploadUrl(BucketName bucketName, string objectName, DateTime validUntil)
    {
        await CreateBucketIfNotExistAsync(bucketName);

        var uploadUrl = publicS3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucketName.ToString(),
            Key = objectName,
            Expires = validUntil,
            Verb = HttpVerb.PUT
        });

        if (IsLocalDevelopment)
            uploadUrl = uploadUrl.Replace("https:", "http:");

        return uploadUrl;
    }

    public async Task<string> GetReadUrl(BucketName bucketName, string objectName, DateTime validUntil)
    {
        await CreateBucketIfNotExistAsync(bucketName);

        var readUrl = publicS3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucketName.ToString(),
            Key = objectName,
            Expires = validUntil,
            Verb = HttpVerb.GET
        });

        if (IsLocalDevelopment)
            readUrl = readUrl.Replace("https:", "http:");

        return readUrl;
    }
}
