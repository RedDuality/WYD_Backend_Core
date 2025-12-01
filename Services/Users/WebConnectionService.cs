using Core.Components.ServerSentMessages;
using Core.Services.Util;

namespace Core.Services.Users;

public class WebConnectionService(IContextManager contextManager, ISseService sseService)
{
    public Task CreateChannel(ISseResponseWriter writer, CancellationToken cancellationToken)
    {
        string userId = contextManager.GetUserId();
        return sseService.CreateChannel(userId, writer, cancellationToken);
    }

}
