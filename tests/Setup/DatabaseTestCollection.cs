using Xunit;

namespace Core.Tests.Setup;

[CollectionDefinition("DatabaseTests")]
public class DatabaseTestCollection : ICollectionFixture<MongoDbFixture>
{
    // No code needed — this class only defines the collection
}

[Collection("DatabaseTests")]
public class DatabaseBootstrapTests
{
    public DatabaseBootstrapTests(MongoDbFixture fixture)
    {
        if (fixture.InitializationFailed)
            throw new Exception(fixture.InitializationError);
    }

    [Fact]
    public void DatabaseBootstrap()
    {
        // Empty test — only exists to surface fixture errors once 
    }
}