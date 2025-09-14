using Core.Model;
using Core.Model.Join;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Components.Database;

public class MongoDbInitializer(
    MongoClient client,
    IMongoDatabase database,
    Action<string>? logger = null
    )
{
    private readonly IMongoDatabase database = database;
    private readonly MongoClient client = client;
    private readonly Action<string> log = logger ?? Console.WriteLine;

    private List<string> collections = [];
    private bool? _isShardingEnabled;


    private async Task<List<string>> ListCollectionsAsync()
    {
        return await database.ListCollectionNames().ToListAsync();
    }

    private string GetDatabaseName()
    {
        return database.DatabaseNamespace.DatabaseName;
    }

    private async Task CheckShardingEnabledAsync()
    {
        if (_isShardingEnabled.HasValue)
            return;

        try
        {
            var result = await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));

            // If the server is a mongos (sharded cluster router), it will return "isdbgrid"
            _isShardingEnabled = result.Contains("msg") && result["msg"] == "isdbgrid";
        }
        catch (Exception ex)
        {
            // Fallback: assume sharding is not enabled if we can't determine it
            _isShardingEnabled = false;
            Console.WriteLine($"Warning: Unable to determine sharding status. Defaulting to false. Details: {ex.Message}");
        }
    }

    public async Task InitAsync()
    {
        log("Initializing MongoDB collections...");

        collections = await ListCollectionsAsync();
        await CheckShardingEnabledAsync();

        await InitializeCollectionAsync(CollectionName.Users, "_id");
        await CreateIndexAsync<User>(CollectionName.Users, "accounts.uid", true);

        await InitializeCollectionAsync(CollectionName.Profiles, "_id");
        await CreateIndexAsync<Profile>(CollectionName.Profiles, "tag", true);
        
        await InitializeCollectionAsync(CollectionName.ProfileDetails, "profileId");

        await InitializeCollectionAsync(CollectionName.ProfileEvents, "profileId");
        await CreateIndexAsync<ProfileEvent>(CollectionName.ProfileEvents, "eventUpdatedAt");
        await CreateIndexAsync<ProfileEvent>(CollectionName.ProfileEvents, "eventStartTime");
        await CreateIndexAsync<ProfileEvent>(CollectionName.ProfileEvents, "eventEndTime");

        await InitializeCollectionAsync(CollectionName.Events, "_id");
        await InitializeCollectionAsync(CollectionName.EventDetails, "eventId");
        await InitializeCollectionAsync(CollectionName.EventMedia, "parentId");
        await CreateIndexAsync<ProfileEvent>(CollectionName.EventMedia, "creationDate");

        await InitializeCollectionAsync(CollectionName.EventProfiles, "eventId");


        log("MongoDB collection initialization complete.");
    }

    private async Task InitializeCollectionAsync(CollectionName cn, string partitionKey)
    {
        string name = cn.ToString();
        if (!collections.Contains(name))
        {
            if (_isShardingEnabled == true)
                await CreateShardedCollectionAsync(name, partitionKey);
            else
                await CreateUnshardedCollectionAsync(name);
        }
    }

    private async Task CreateShardedCollectionAsync(string name, string key)
    {
        log($"Attempting to shard collection '{GetDatabaseName()}.{name}'...");
        var adminDb = client.GetDatabase("admin");

        var command = new BsonDocument
        {
            { "shardCollection", $"{GetDatabaseName()}.{name}" },
            { "key", new BsonDocument { { key, "hashed" } } }
        };

        try
        {
            await adminDb.RunCommandAsync<BsonDocument>(command);
            log($"Collection '{GetDatabaseName()}.{name}' sharded successfully.");
        }
        catch (MongoCommandException ex) when (ex.Code == 292 || ex.Message.Contains("already sharded"))
        {
            log($"Collection '{GetDatabaseName()}.{name}' is already sharded.");
        }
    }

    private async Task CreateUnshardedCollectionAsync(string collectionName)
    {
        var dbName = GetDatabaseName();
        log($"Creating unsharded collection '{dbName}.{collectionName}'...");

        try
        {
            await database.CreateCollectionAsync(collectionName);
            log($"Collection '{collectionName}' created successfully (unsharded).");
        }
        catch (MongoCommandException ex) when (
            ex.Code == 48 ||
            ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            log($"Collection '{collectionName}' already exists (unsharded). No action needed.");
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Error creating unsharded collection '{collectionName}' in database '{dbName}': {ex.Message}",
                ex
            );
        }
    }


    private async Task CreateIndexAsync<TDocument>(
        CollectionName collectionName,
        string fieldName,
        bool isUnique = false
    )
    {
        var name = collectionName.ToString();
        var collection = database.GetCollection<TDocument>(name);

        var indexKeys = Builders<TDocument>.IndexKeys.Ascending(fieldName);
        var indexOptions = new CreateIndexOptions { Unique = isUnique };
        var indexModel = new CreateIndexModel<TDocument>(indexKeys, indexOptions);

        try
        {
            await collection.Indexes.CreateOneAsync(indexModel);
            log($"Index created on '{fieldName}' in collection '{name}' (Unique: {isUnique}).");
        }
        catch (MongoWriteException ex) when (
            ex.Message.Contains("Index already exists", StringComparison.OrdinalIgnoreCase))
        {
            log($"Index on '{fieldName}' in collection '{name}' already exists. Skipping.");
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB error while creating index on '{fieldName}' in collection '{name}': {ex.Message}",
                ex
            );
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Unexpected error while creating index on '{fieldName}' in collection '{name}': {ex.Message}",
                ex
            );
        }
    }

}
