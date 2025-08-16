
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Servers;

using Microsoft.Extensions.Configuration;

using Core.Model;
using Core.Model.Base;
using Core.Model.Join;


namespace Core.Services.Database;

public class MongoDbContext
{
    private readonly MongoClient client;
    public readonly IMongoDatabase database;

    private bool? _isShardingEnabled;
    private bool? _transactionsSupported;

    public MongoDbContext(IConfiguration configuration)
    {
        BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));

        // Use the individual environment variables to construct the connection string
        string username = configuration.GetValue<string>("MONGODB_APP_USER")
            ?? throw new Exception("Database connection failed: 'MONGODB_APP_USER' is not set in configuration.");
        
        string password = configuration.GetValue<string>("MONGODB_APP_PASSWORD")
            ?? throw new Exception("Database connection failed: 'MONGODB_APP_PASSWORD' is not set in configuration.");
        
        string hostname = configuration.GetValue<string>("MONGODB_HOSTNAME")
            ?? throw new Exception("Database connection failed: 'MONGODB_HOSTNAME' is not set in configuration.");
        
        string databaseName = configuration.GetValue<string>("DATABASE_NAME")
            ?? throw new InvalidOperationException("Database connection failed: 'DATABASENAME' is not set in configuration.");
        
        // Build the connection string from the individual components
        string connectionString = $"mongodb://{username}:{password}@{hostname}:27017/{databaseName}?authSource=admin";
        
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



    public async Task<IClientSessionHandle> GetNewSession()
    {
        return await client.StartSessionAsync();
    }

    public IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName)
    where TDocument : BaseEntity
    {
        return database.GetCollection<TDocument>(collectionName);
    }

    public bool AreTransactionsSupported()
    {
        // Cache the result after the first check to avoid repeated server topology checks
        if (_transactionsSupported.HasValue)
            return _transactionsSupported.Value;


        // Get the current server description
        ServerDescription? serverDescription = null;
        var servers = client.Cluster.Description.Servers;

        if (servers.Count > 0)
            serverDescription = servers[0];

        if (serverDescription != null)
        {
            // Check the server type for transaction support
            // Transactions are supported on ReplicaSetPrimary, ReplicaSetSecondary, Sharded, and Unknown (if it eventually connects to one of the above)
            // They are NOT supported on Standalone.
            _transactionsSupported = serverDescription.Type switch
            {
                ServerType.ReplicaSetPrimary => true,
                ServerType.ReplicaSetSecondary => true,
                ServerType.ShardRouter => true, // MongoDB 4.2+ supports transactions on sharded clusters
                ServerType.Standalone => false, // Standalone servers do not support transactions
                ServerType.Unknown => false, // If the server type is unknown, assume no transaction support for safety
                _ => false // Default to false for any other unexpected server types
            };
        }
        else
        {
            _transactionsSupported = false;
            Console.WriteLine("Warning: Could not determine MongoDB server type. Assuming transactions are not supported.");
        }

        return _transactionsSupported.Value;
    }


    #region init

    public async Task Init()
    {
        Console.WriteLine("Initializing MongoDB collections...");

        var collectionNames = await ListCollectionsAsync();

        await CheckShardingEnabledAsync();

        if (!collectionNames.Contains(CollectionName.Users.ToString()))
            await InitializeCollectionAsync(CollectionName.Users, "_id");

        await CreateIndexAsync<User>(CollectionName.Users, "accounts.uid", true);

        if (!collectionNames.Contains(CollectionName.Profiles.ToString()))
            await InitializeCollectionAsync(CollectionName.Profiles, "_id");

        if (!collectionNames.Contains(CollectionName.ProfileDetails.ToString()))
            await InitializeCollectionAsync(CollectionName.ProfileDetails, "profileId");

        if (!collectionNames.Contains(CollectionName.ProfileEvents.ToString()))
            await InitializeCollectionAsync(CollectionName.ProfileEvents, "profileId");

        await CreateIndexAsync<ProfileEvent>(CollectionName.ProfileEvents, "eventUpdatedAt");
        await CreateIndexAsync<ProfileEvent>(CollectionName.ProfileEvents, "eventStartTime");
        await CreateIndexAsync<ProfileEvent>(CollectionName.ProfileEvents, "eventEndTime");


        if (!collectionNames.Contains(CollectionName.EventProfiles.ToString()))
            await InitializeCollectionAsync(CollectionName.ProfileEvents, "eventId");

        if (!collectionNames.Contains(CollectionName.Events.ToString()))
            await InitializeCollectionAsync(CollectionName.Events, "_id");

        if (!collectionNames.Contains(CollectionName.Images.ToString()))
            await InitializeCollectionAsync(CollectionName.Images, "eventId");

        Console.WriteLine("MongoDB collection initialization complete.");
    }

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

        var configDatabase = client.GetDatabase("config");
        try
        {
            var databasesCollection = configDatabase.GetCollection<BsonDocument>("databases");
            var filter = Builders<BsonDocument>.Filter.Eq("_id", GetDatabaseName());
            var dbInfo = await databasesCollection.Find(filter).FirstOrDefaultAsync();

            _isShardingEnabled = dbInfo != null && dbInfo.TryGetValue("partitioned", out var partitionedValue) && partitionedValue.AsBoolean;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not determine sharding status for database '{GetDatabaseName}'. Error: {ex.Message}");
            // If we can't determine, assume not sharded or proceed with sharding attempt and let it fail
            _isShardingEnabled = false;
        }
    }

    public async Task InitializeCollectionAsync(CollectionName cn, string partitionKeyFieldName)
    {
        string collectionName = cn.ToString();
        if (_isShardingEnabled is true)
            await CreateShardedCollectionAsync(collectionName, partitionKeyFieldName);
        else
            await CreateUnshardedCollectionAsync(collectionName);
    }

    private async Task CreateShardedCollectionAsync(string collectionName, string partitionKeyFieldName)
    {
        Console.WriteLine($"Attempting to shard collection '{GetDatabaseName()}.{collectionName}'...");
        try
        {
            var adminDatabase = client.GetDatabase("admin");

            var shardCommand = new BsonDocument
            {
                { "shardCollection", $"{GetDatabaseName()}.{collectionName}" },
                { "key", new BsonDocument { { partitionKeyFieldName, "hashed" } } },
            };
            await adminDatabase.RunCommandAsync<BsonDocument>(shardCommand);
        }
        catch (MongoCommandException ex)
        {
            if (ex.Code == 292 || ex.Message.Contains("already sharded", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"Collection '{GetDatabaseName()}.{collectionName}' is already sharded. No action needed."
                );
                return;
            }
            else
            {
                throw new Exception(
                    $"Failed to shard the collection '{GetDatabaseName()}.{collectionName}' due to an unexpected MongoDB command error: {ex.Message}",
                    ex
                );
            }
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"An unexpected error occurred during collection initialization for '{GetDatabaseName()}.{collectionName}'.",
                ex
            );
        }

        Console.WriteLine($"Collection '{GetDatabaseName()}.{collectionName}' sharded successfully.");
    }

    private async Task CreateUnshardedCollectionAsync(string collectionName)
    {
        Console.WriteLine($"Creating unsharded collection '{GetDatabaseName()}.{collectionName}'.");
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

    private async Task CreateIndexAsync<TDocument>(
        CollectionName cn,
        string fieldName,
        bool isUnique = false
    )
    where TDocument : BaseEntity
    {
        string collectionName = cn.ToString();

        try
        {
            var collection = GetCollection<TDocument>(collectionName);

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

    #endregion

}
