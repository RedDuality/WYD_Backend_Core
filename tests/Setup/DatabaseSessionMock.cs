using MongoDB.Driver;
using Moq;

namespace Core.Tests.Setup;

public static class DatabaseSessionMock
{
    public static IClientSessionHandle Dummy()
        => new Mock<IClientSessionHandle>().Object;
}
