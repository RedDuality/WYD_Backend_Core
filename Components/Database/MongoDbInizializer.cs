using Core.Model.MediaStorage;
using Core.Model.Profiles;
using Core.Model.Users;
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
            if (result.TryGetValue("msg", out var msg) && msg == "isdbgrid")
                _isShardingEnabled = true;
            else
                _isShardingEnabled = false;

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

        // User
        await InitializeCollectionAsync(CollectionName.Users, "_id");
        // create and retrieve accounts/users
        await CreateCompoundIndexAsync<User>(
            CollectionName.Users,
            [("_id", 1), ("accounts.uid", 1)],
            isUnique: true
        );

        await InitializeCollectionAsync(CollectionName.UserClaims, "userId");
        // save and retrieve the user's claims
        await CreateCompoundIndexAsync<UserClaims>(
            CollectionName.UserClaims,
            [("userId", 1), ("profileId", 1)],
            isUnique: true
        );

        // Profile
        await InitializeCollectionAsync(CollectionName.Profiles, "_id");

        await InitializeCollectionAsync(CollectionName.ProfileTags, "_id", doNotShard: true);
        await CreateIndexAsync<ProfileTag>(CollectionName.ProfileTags, "tag", true);
        await CreateIndexAsync<ProfileTag>(CollectionName.ProfileTags, "profileId", true);

        await InitializeCollectionAsync(CollectionName.ProfileDetails, "profileId");

        await InitializeCollectionAsync(CollectionName.ProfileEvents, "profileId");
        // retrieveEvents
        await CreateCompoundIndexAsync<ProfileEvent>(
            CollectionName.ProfileEvents,
            [("profileId", 1), ("eventEndTime", 1), ("eventStartTime", 1)]
        );
        // create profileEvent, ensure uniqueness
        await CreateCompoundIndexAsync<ProfileEvent>(
            CollectionName.ProfileEvents,
            [("profileId", 1), ("eventId", 1)],
            isUnique: true
        );
        // propagate updates
        await CreateCompoundIndexAsync<ProfileEvent>(
            CollectionName.ProfileEvents,
            [("profileId", 1), ("eventId", 1), ("eventUpdatedAt", -1)]
        );
        // retrieve updates
        await CreateCompoundIndexAsync<ProfileEvent>(
            CollectionName.ProfileEvents,
            [("profileId", 1), ("updatedAt", 1)]
        );

        await InitializeCollectionAsync(CollectionName.ProfileCommunities, "profileId");
        await CreateIndexAsync<ProfileCommunity>(CollectionName.ProfileCommunities, "communityUpdatedAt");
        await CreateIndexAsync<ProfileCommunity>(CollectionName.ProfileCommunities, "otherProfileId");
        await CreateIndexAsync<ProfileCommunity>(CollectionName.ProfileCommunities, "communityId");

        // Event
        await InitializeCollectionAsync(CollectionName.Events, "_id");

        await InitializeCollectionAsync(CollectionName.EventDetails, "eventId");

        await InitializeCollectionAsync(CollectionName.EventMedia, "parentId");
        await CreateIndexAsync<Media>(CollectionName.EventMedia, "creationDate");

        await InitializeCollectionAsync(CollectionName.EventProfiles, "eventId");
        await CreateCompoundIndexAsync<ProfileEvent>(
            CollectionName.EventProfiles,
            [("eventId", 1), ("profileId", 1)],
            isUnique: true
        );

        // Community
        await InitializeCollectionAsync(CollectionName.Communities, "_id");

        await InitializeCollectionAsync(CollectionName.Groups, "communityId");

        await InitializeCollectionAsync(CollectionName.CommunityProfiles, "communityId");


        log("MongoDB collection initialization complete.");
    }

    private async Task InitializeCollectionAsync(CollectionName cn, string partitionKey, bool doNotShard = false)
    {
        string name = cn.ToString();
        if (!collections.Contains(name))
        {
            if (_isShardingEnabled == true && !doNotShard)
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
        bool isUnique = false,
        IndexKeysDefinition<TDocument>? indexkey = null
    )
    {
        var name = collectionName.ToString();
        var collection = database.GetCollection<TDocument>(name);

        var indexKeys = indexkey ?? Builders<TDocument>.IndexKeys.Ascending(fieldName);
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

    private async Task CreateCompoundIndexAsync<TDocument>(
    CollectionName collectionName,
    IEnumerable<(string FieldName, int Order)> fields,
    bool isUnique = false
    )
    {
        var name = collectionName.ToString();
        var collection = database.GetCollection<TDocument>(name);

        // Build compound index keys
        var indexKeysList = new List<IndexKeysDefinition<TDocument>>();
        foreach (var (fieldName, order) in fields)
        {
            var key = order >= 0
                ? Builders<TDocument>.IndexKeys.Ascending(fieldName)
                : Builders<TDocument>.IndexKeys.Descending(fieldName);

            indexKeysList.Add(key);
        }

        var indexKeys = Builders<TDocument>.IndexKeys.Combine(indexKeysList);
        var indexOptions = new CreateIndexOptions { Unique = isUnique };
        var indexModel = new CreateIndexModel<TDocument>(indexKeys, indexOptions);

        try
        {
            await collection.Indexes.CreateOneAsync(indexModel);
            log($"Compound index created on [{string.Join(", ", fields.Select(f => f.FieldName))}] in collection '{name}' (Unique: {isUnique}).");
        }
        catch (MongoWriteException ex) when (
            ex.Message.Contains("Index already exists", StringComparison.OrdinalIgnoreCase))
        {
            log($"Compound index on [{string.Join(", ", fields.Select(f => f.FieldName))}] in collection '{name}' already exists. Skipping.");
        }
        catch (MongoException ex)
        {
            throw new Exception(
                $"MongoDB error while creating compound index on [{string.Join(", ", fields.Select(f => f.FieldName))}] in collection '{name}': {ex.Message}",
                ex
            );
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Unexpected error while creating compound index on [{string.Join(", ", fields.Select(f => f.FieldName))}] in collection '{name}': {ex.Message}",
                ex
            );
        }
    }


}
