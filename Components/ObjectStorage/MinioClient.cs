using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace Core.Components.ObjectStorage;

public class MinioClient
{

    private readonly HashSet<string> checkedBuckets = [];
    private readonly AmazonS3Client s3Client;
    private readonly string publicEndpoint;

    public MinioClient(IConfiguration configuration)
    {
        string MinioEndpoint = configuration.GetValue<string>("OBJ_STORAGE_ENDPOINT")
            ?? throw new Exception("Object Storage connection failed: 'OBJ_STORAGE_ENDPOINT' is not set in configuration.");
        string MinioAppUser = configuration.GetValue<string>("OBJ_STORAGE_USER")
            ?? throw new Exception("Object Storage connection failed: 'OBJ_STORAGE_USER' is not set in configuration.");
        string MinioAppPassword = configuration.GetValue<string>("OBJ_STORAGE_PASSWORD")
            ?? throw new Exception("Object Storage connection failed: 'OBJ_STORAGE_PASSWORD' is not set in configuration.");
        publicEndpoint = configuration.GetValue<string>("OBJ_STORAGE_PUBLIC_ENDPOINT")
        ?? throw new Exception("Object Storage connection failed: 'OBJ_STORAGE_PUBLIC_ENDPOINT' is not set in configuration.");


        var s3Config = new AmazonS3Config
        {
            ServiceURL = MinioEndpoint,

            // Required for MinIO
            ForcePathStyle = true
        };

        s3Client = new AmazonS3Client(MinioAppUser, MinioAppPassword, s3Config);
    }

    public async Task TestConnection()
    {

        // Attempt to list buckets. A successful response indicates a valid connection.
        var response = await s3Client.ListBucketsAsync();
        Console.WriteLine("Successfully connected to MinIO service." + response.Buckets);
        //return  != null;

    }

    private async Task CreateBucketIfNotExistAsync(BucketName bn)
    {
        string bucketName = bn.ToString();
        if (checkedBuckets.Contains(bucketName)) return;

        try
        {
            // 1. Check if the bucket exists by listing all buckets
            var listBucketsResponse = await s3Client.ListBucketsAsync();

            // Add a null check for the response and the Buckets property
            bool bucketExists = listBucketsResponse != null && listBucketsResponse.Buckets != null && listBucketsResponse.Buckets.Any(b => b.BucketName == bucketName);

            if (!bucketExists)
            {
                // 2. If the bucket doesn't exist, create it
                var putBucketRequest = new PutBucketRequest
                {
                    BucketName = bucketName
                };
                await s3Client.PutBucketAsync(putBucketRequest);
                Console.WriteLine($"Bucket '{bucketName}' created successfully.");
            }
            else
            {
                Console.WriteLine($"Bucket '{bucketName}' already exists.");
            }

            checkedBuckets.Add(bucketName);
        }
        catch
        {
            throw new Exception($"There was an error while trying to create the Bucket");
        }
    }
    public async Task<string> GetUploadUrl(BucketName bucketName, string ObjectName, DateTime validUntil)
    {
        try
        {
            await CreateBucketIfNotExistAsync(bucketName);

            // Generate the pre-signed URL for the upload.
            string uploadUrl = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = bucketName.ToString(),
                Key = ObjectName,
                Expires = validUntil,
                Verb = HttpVerb.PUT,
            });

            uploadUrl = uploadUrl.Replace("https:", "http:");
            var internalEndpoint = s3Client.Config.ServiceURL;
            uploadUrl = uploadUrl.Replace(internalEndpoint, publicEndpoint);

            return uploadUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            throw new Exception(
                "There was an error while trying to connect to the Object Storage",
                ex
            );
        }
    }

    public async Task<string> GetReadUrl(BucketName bucketName, string ObjectName, DateTime validUntil)
    {
        try
        {
            await CreateBucketIfNotExistAsync(bucketName);

            // Generate the pre-signed URL for the upload.
            string readUrl = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = bucketName.ToString(),
                Key = ObjectName,
                Expires = validUntil,
                Verb = HttpVerb.GET
            });

            readUrl = readUrl.Replace("https:", "http:");
            var internalEndpoint = s3Client.Config.ServiceURL;
            readUrl = readUrl.Replace(internalEndpoint, publicEndpoint);

            return readUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            throw new Exception(
                "There was an error while trying to connect to the Object Storage",
                ex
            );
        }
    }

}
