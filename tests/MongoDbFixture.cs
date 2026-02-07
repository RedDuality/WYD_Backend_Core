using Testcontainers.MongoDb;
using Microsoft.Extensions.Configuration;
using Core.Components.Database;
using Xunit;

public class MongoDbFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder("mongo:7.0").Build();

    public MongoDbContext DbContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _mongoContainer.StartAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MONGODB_HOSTNAME"] = _mongoContainer.GetConnectionString(),
                ["MONGODB_APP_USER"] = "root",
                ["MONGODB_APP_PASSWORD"] = "password",
                ["DATABASE_NAME"] = "TestDb"
            })
            .Build();

        DbContext = new MongoDbContext(config);
        await DbContext.Init();
    }

    public async Task DisposeAsync() => await _mongoContainer.StopAsync();
}
