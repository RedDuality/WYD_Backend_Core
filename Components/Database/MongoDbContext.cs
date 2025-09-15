
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Servers;

using Microsoft.Extensions.Configuration;

using Core.Model.Base;


namespace Core.Components.Database;

public class MongoDbContext
{
    private readonly MongoClient client;
    private readonly IMongoDatabase database;

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
            ?? throw new InvalidOperationException("Database connection failed: 'DATABASE_NAME' is not set in configuration.");

        // Build the connection string from the individual components
        string connectionString = $"mongodb://{username}:{password}@{hostname}/{databaseName}?authSource=admin";

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

    public async Task TestConnection()
    { 
        await database.ListCollectionNames().ToListAsync();
    }
    public async Task Init()
    {
        var initializer = new MongoDbInitializer(
            client,
            database,
            Console.WriteLine
        );

        await initializer.InitAsync();
    }


}
