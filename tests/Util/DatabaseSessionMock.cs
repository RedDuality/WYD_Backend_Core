using MongoDB.Driver;
using Moq;

namespace Core.Tests.Util;

public static class DatabaseSessionMock
{
    public static IClientSessionHandle Dummy()
        => new Mock<IClientSessionHandle>().Object;
}
