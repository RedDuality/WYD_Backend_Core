using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using NRModel;

namespace Database;

public class CosmosDbContext
{
    private readonly MongoClient client;

    public readonly IMongoDatabase database;

    public CosmosDbContext()
    {
        BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));

        string? connectionString = Environment.GetEnvironmentVariable("CosmosConnectionString");
        string databaseName = "wydCentral";

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("Database connection failed: no connection string");
        }

        if (string.IsNullOrEmpty(databaseName))
        {
            throw new InvalidOperationException(
                "Database connection failed: CosmosDatabaseName is not set."
            );
        }

        try
        {
            client = new MongoClient(connectionString);
            database = client.GetDatabase(databaseName);
        }
        catch (Exception ex)
        {
            throw new Exception(
                "Database connection failed: there was an error while trying to connect",
                ex
            );
        }
    }

    public async Task<List<string>> CheckConnection()
    {
        try
        {
            return await (await client.ListDatabaseNamesAsync()).ToListAsync();
        }
        catch (Exception ex)
        {
            throw new Exception("An unexpected error occurred during connection check.", ex);
        }
    }

    public async Task<IClientSessionHandle> GetNewSession()
    {
        return await client.StartSessionAsync();
    }

    public IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName)
    where TDocument : BaseEntity
    {
        return database.GetCollection<TDocument>(collectionName);
    }

    public async Task Init()
    {
        //await InitializeCollectionAsync("Events", "_id");

        //await InitializeCollectionAsync("Profiles", "_id");

        await InitializeCollectionAsync("ProfileEvents", "profileId");
        await CreateIndexAsync<ProfileEventDocument>("ProfileEvents", "eventUpdatedAt");
        await CreateIndexAsync<ProfileEventDocument>("ProfileEvents", "eventStartTime");
        await CreateIndexAsync<ProfileEventDocument>("ProfileEvents", "eventEndTime");

        await InitializeCollectionAsync("EventProfiles", "eventId");

        //await InitializeCollectionAsync("Images", "eventId");
    }

    private async Task InitializeCollectionAsync(string collectionName, string partitionKeyFieldName)
    {
        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentException(
                "Collection name cannot be null or empty.",
                nameof(collectionName)
            );
        }
        if (string.IsNullOrWhiteSpace(partitionKeyFieldName))
        {
            throw new ArgumentException(
                "Partition key field name cannot be null or empty.",
                nameof(partitionKeyFieldName)
            );
        }

        var adminDatabase = client.GetDatabase("admin");

        var shardCommand = new BsonDocument
        {
            { "shardCollection", $"{database.DatabaseNamespace.DatabaseName}.{collectionName}" },
            {
                "key",
                new BsonDocument { { partitionKeyFieldName, "hashed" } }
            },
        };

        try
        {
            var result = await adminDatabase.RunCommandAsync<BsonDocument>(shardCommand);
        }
        catch (MongoCommandException ex)
        {
            if (ex.Code == 292 || ex.Message.Contains("already sharded"))
            {
                Console.WriteLine(
                    $"Collection '{database.DatabaseNamespace.DatabaseName}.{collectionName}' is already sharded. No action needed."
                );
            }
            else if (ex.Message.Contains("a collection") && ex.Message.Contains("already exists"))
            {
                throw new Exception(
                    $"Failed to shard the collection '{database.DatabaseNamespace.DatabaseName}.{collectionName}'. It already exists in an unsharded form, and cannot be sharded without specific data migration/conversion steps (or was implicitly created by a prior operation without sharding).",
                    ex
                );
            }
            else
            {
                throw new Exception(
                    $"Failed to shard the collection '{database.DatabaseNamespace.DatabaseName}.{collectionName}'. Ensure sharding is enabled for the database.",
                    ex
                );
            }
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"An unexpected error occurred during sharding for collection '{database.DatabaseNamespace.DatabaseName}.{collectionName}'.",
                ex
            );
        }
    }

    private async Task CreateIndexAsync<TDocument>(
        string collectionName,
        string fieldName,
        bool isUnique = false
    )
    where TDocument : BaseEntity
    {
        try
        {
            IMongoCollection<TDocument> collection = database.GetCollection<TDocument>(
                collectionName
            );
            var indexKeys = Builders<TDocument>.IndexKeys.Ascending(fieldName);
            var indexOptions = new CreateIndexOptions { Unique = isUnique };
            var indexModel = new CreateIndexModel<TDocument>(indexKeys, indexOptions);

            await collection.Indexes.CreateOneAsync(indexModel);
        }
        catch (MongoWriteException ex) when (ex.Message.Contains("Index already exists")) { }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB operation failed while creating index on field '{fieldName}' for collection '{collectionName}'.",
                ex
            );
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"An unexpected error occurred while creating index on field '{fieldName}' for collection '{collectionName}'.",
                ex
            );
        }
    }
}
