namespace Core.Components.ServerSentMessages;

public interface ISseResponseWriter
{
    void SetHeaders(IDictionary<string, string> headers);
    Task WriteAsync(string payload, CancellationToken cancellationToken);
    Task FlushAsync(CancellationToken cancellationToken);
}

public interface ISseService
{
    /// <summary>
    /// Creates an SSE channel for the given user and writes the stream to the response.
    /// </summary>
    Task CreateChannel(string userId, ISseResponseWriter responseWriter, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a message to a single user.
    /// </summary>
    void SendToUser(string userId, string message);

    /// <summary>
    /// Sends a message to multiple users.
    /// </summary>
    void SendToUsers(HashSet<string> userIds, string message);
}

