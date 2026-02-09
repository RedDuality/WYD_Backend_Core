using Testcontainers.MongoDb;
using Microsoft.Extensions.Configuration;
using Core.Components.Database;
using Xunit;
using System.Runtime.InteropServices;

namespace Core.Tests.Setup;

public class MongoDbFixture : IAsyncLifetime
{
    private MongoDbContainer? _mongoContainer;

    public MongoDbService? DbService { get; private set; }
    public IServiceProvider? ServiceProvider { get; private set; }

    public bool InitializationFailed { get; private set; }
    public string? InitializationError { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            if (!IsDockerProviderAvailable())
            {
                throw new Exception("Docker is not running or the socket (npipe/sock) is unreachable.");
            }

            _mongoContainer = new MongoDbBuilder("mongo:8.2.4")
                .WithUsername("root")
                .WithPassword("password")
                .Build();

            await _mongoContainer.StartAsync();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MONGODB_HOSTNAME"] = _mongoContainer.Hostname,
                    ["MONGODB_PORT"] = _mongoContainer.GetMappedPublicPort(27017).ToString(),
                    ["MONGODB_APP_USER"] = "root",
                    ["MONGODB_APP_PASSWORD"] = "password",
                    ["DATABASE_NAME"] = "TestDb"
                })
                .Build();

            var dbContext = new MongoDbContext(config);
            await dbContext.Init();

            DbService = new MongoDbService(dbContext);
            ServiceProvider = TestServiceFactory.CreateServiceProvider(DbService);
        }
        catch (Exception ex)
        {
            InitializationFailed = true;
            InitializationError = ex.Message;
        }
    }

    public async Task DisposeAsync()
    {
        if (_mongoContainer != null)
        {
            try { await _mongoContainer.DisposeAsync(); }
            catch { /* ignore cleanup errors */ }
        }
    }

    public static bool IsDockerProviderAvailable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return File.Exists(@"\\.\pipe\docker_engine");

        return File.Exists("/var/run/docker.sock");
    }
}
