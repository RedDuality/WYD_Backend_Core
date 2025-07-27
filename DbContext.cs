using Core.Model;
using Core.Model.Base;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Microsoft.Extensions.Configuration;

using MongoDB.Driver;

namespace Core;

public class MongoDbContext
{
    private readonly MongoClient client;
    public readonly IMongoDatabase database;

    public MongoDbContext(IConfiguration configuration)
    {
        BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));

        string connectionString = configuration.GetConnectionString("MongoDB")
            ?? throw new Exception("Database connection failed: 'ConnectionStrings:MongoDB' is not set in configuration.");

        string databaseName = configuration.GetValue<string>("MongoDB:DatabaseName")
            ?? throw new InvalidOperationException("Database connection failed: 'MongoDB:DatabaseName' is not set in configuration.");

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
        Console.WriteLine("Initializing MongoDB collections...");

        var collectionNames = await database.ListCollectionNames().ToListAsync();
        var isShardingEnabled = await IsDatabaseShardingEnabledAsync(database.DatabaseNamespace.DatabaseName);

        if (!collectionNames.Contains("Events"))
            await InitializeCollectionAsync("Events", "_id", isShardingEnabled);

        if (!collectionNames.Contains("Profiles"))
            await InitializeCollectionAsync("Profiles", "_id", isShardingEnabled);

        if (!collectionNames.Contains("ProfileEvents"))
            await InitializeCollectionAsync("ProfileEvents", "profileId", isShardingEnabled);

        await CreateIndexAsync<ProfileEvent>("ProfileEvents", "eventUpdatedAt");
        await CreateIndexAsync<ProfileEvent>("ProfileEvents", "eventStartTime");
        await CreateIndexAsync<ProfileEvent>("ProfileEvents", "eventEndTime");


        if (!collectionNames.Contains("EventProfiles"))
            await InitializeCollectionAsync("EventProfiles", "eventId", isShardingEnabled);

        if (!collectionNames.Contains("Images"))
            await InitializeCollectionAsync("Images", "eventId", isShardingEnabled);

        Console.WriteLine("MongoDB collection initialization complete.", isShardingEnabled);
    }

    private async Task<bool> IsDatabaseShardingEnabledAsync(string databaseName)
    {
        var configDatabase = client.GetDatabase("config");
        try
        {
            var databasesCollection = configDatabase.GetCollection<BsonDocument>("databases");
            var filter = Builders<BsonDocument>.Filter.Eq("_id", databaseName);
            var dbInfo = await databasesCollection.Find(filter).FirstOrDefaultAsync();

            return dbInfo != null && dbInfo.TryGetValue("partitioned", out var partitionedValue) && partitionedValue.AsBoolean;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not determine sharding status for database '{databaseName}'. Error: {ex.Message}");
            // If we can't determine, assume not sharded or proceed with sharding attempt and let it fail
            return false;
        }
    }


    public async Task InitializeCollectionAsync(string collectionName, string partitionKeyFieldName, bool sharding)
    {
        if (sharding)
            await CreateShardedCollectionAsync(collectionName, partitionKeyFieldName);
        else
            await CreateUnshardedCollectionAsync(collectionName);
    }

    private async Task CreateShardedCollectionAsync(string collectionName, string partitionKeyFieldName)
    {
        Console.WriteLine($"Attempting to shard collection '{database.DatabaseNamespace.DatabaseName}.{collectionName}'...");
        try
        {
            var adminDatabase = client.GetDatabase("admin");

            var shardCommand = new BsonDocument
            {
                { "shardCollection", $"{database.DatabaseNamespace.DatabaseName}.{collectionName}" },
                { "key", new BsonDocument { { partitionKeyFieldName, "hashed" } } },
            };
            await adminDatabase.RunCommandAsync<BsonDocument>(shardCommand);
        }
        catch (MongoCommandException ex)
        {
            if (ex.Code == 292 || ex.Message.Contains("already sharded", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"Collection '{database.DatabaseNamespace.DatabaseName}.{collectionName}' is already sharded. No action needed."
                );
                return;
            }
            else
            {
                throw new Exception(
                    $"Failed to shard the collection '{database.DatabaseNamespace.DatabaseName}.{collectionName}' due to an unexpected MongoDB command error: {ex.Message}",
                    ex
                );
            }
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"An unexpected error occurred during collection initialization for '{database.DatabaseNamespace.DatabaseName}.{collectionName}'.",
                ex
            );
        }

        Console.WriteLine($"Collection '{database.DatabaseNamespace.DatabaseName}.{collectionName}' sharded successfully.");
    }

    private async Task CreateUnshardedCollectionAsync(string collectionName)
    {
        Console.WriteLine($"Creating unsharded collection '{database.DatabaseNamespace.DatabaseName}.{collectionName}'.");
        try
        {
            await database.CreateCollectionAsync(collectionName);
        }
        catch (MongoCommandException ex)
        {
            if (ex.Code == 48 || ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Collection '{collectionName}' already exists (unsharded). No action needed.");
            }
            else
            {
                throw new Exception($"Failed to create unsharded collection '{collectionName}': {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"An unexpected error occurred while creating unsharded collection '{collectionName}': {ex.Message}", ex);
        }

        Console.WriteLine($"Collection '{collectionName}' ensured to exist (unsharded).");
    }

    /*
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
    */

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
