using Core.Model.Masks;
using Core.Model.MediaStorage;
using Core.Model.Profiles;
using Core.Model.Users;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Components.Database;

public class MongoDbInitializerService(
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

    public async Task Init()
    {
        collections = await ListCollectionsAsync();
        await CheckShardingEnabledAsync();
    }
    public async Task CheckShardingEnabledAsync()
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

    public async Task InitializeCollectionAsync(CollectionName cn, string partitionKey, bool doNotShard = false)
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

    public async Task CreateShardedCollectionAsync(string name, string key)
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

    public async Task CreateUnshardedCollectionAsync(string collectionName)
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


    public async Task CreateIndexAsync<TDocument>(
        CollectionName collectionName,
        string fieldName,
        bool isUnique = false,
        BsonDocument? partialFilter = null,
        IndexKeysDefinition<TDocument>? indexkey = null
    )
    {
        var name = collectionName.ToString();
        var collection = database.GetCollection<TDocument>(name);

        var indexKeys = indexkey ?? Builders<TDocument>.IndexKeys.Ascending(fieldName);
        var indexOptions = new CreateIndexOptions<TDocument> { Unique = isUnique, PartialFilterExpression = partialFilter };
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

    public async Task CreateCompoundIndexAsync<TDocument>(
    CollectionName collectionName,
    IEnumerable<(string FieldName, int Order)> fields,
    bool isUnique = false,
    BsonDocument? partialFilter = null
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
        var indexOptions = new CreateIndexOptions<TDocument> { Unique = isUnique, PartialFilterExpression = partialFilter };
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
