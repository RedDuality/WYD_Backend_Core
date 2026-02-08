using Testcontainers.MongoDb;
using Microsoft.Extensions.Configuration;
using Core.Components.Database;
using Xunit;

namespace Core.Tests;

public class MongoDbFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder("mongo:8.2.4")
        .WithUsername("root")
        .WithPassword("password")
        .Build();

    public MongoDbContext DbContext { get; private set; } = null!;

    // docker must be running and user must be in the docker group
    //
    // create docker group
    // sudo groupadd docker
    // sudo usermod -aG docker $USER
    //
    // start docker
    // sudo systemctl start docker
    public async Task InitializeAsync()
    {
        Console.WriteLine("Starting Mongo Container on Docker");
        await _mongoContainer.StartAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                // Use the container properties instead of the full connection string
                ["MONGODB_HOSTNAME"] = _mongoContainer.Hostname,
                ["MONGODB_PORT"] = _mongoContainer.GetMappedPublicPort(27017).ToString(),
                ["MONGODB_APP_USER"] = "root", // Matches your current setup
                ["MONGODB_APP_PASSWORD"] = "password", // Matches your current setup
                ["DATABASE_NAME"] = "TestDb"
            })
            .Build();

        DbContext = new MongoDbContext(config);
        await DbContext.Init();

        Console.WriteLine("Database Initialized");

    }

    public async Task DisposeAsync() => await _mongoContainer.StopAsync();
}
