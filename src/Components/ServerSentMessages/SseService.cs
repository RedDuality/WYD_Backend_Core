using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Core.Components.ServerSentMessages;

public class SseService : ISseService
{
    private readonly ConcurrentDictionary<string, Channel<string>> _channels = new();

    public async Task CreateChannel(string userId, ISseResponseWriter writer, CancellationToken ct)
    {
        var channel = _channels.GetOrAdd(userId, _ => Channel.CreateUnbounded<string>());

        writer.SetHeaders(new Dictionary<string, string>
        {
            ["Content-Type"] = "text/event-stream",
            ["Cache-Control"] = "no-cache",
            ["Connection"] = "keep-alive",
        });

        try
        {
            await foreach (var message in channel.Reader.ReadAllAsync(ct))
            {
                Console.WriteLine($"sending message to web application:{message}");
                var payload = $"data: {message}\n\n";
                await writer.WriteAsync(payload, ct);
                await writer.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _channels.TryRemove(userId, out _);
        }
    }

    public void SendToUser(string userId, string message)
    {
        Console.WriteLine($"send to user{message}");
        if (_channels.TryGetValue(userId, out var channel))
            channel.Writer.TryWrite(message);
    }

    public void SendToUsers(HashSet<string> userIds, string message)
    {
        foreach (var id in userIds)
            SendToUser(id, message);
    }
}


/* PubSub Implementation

public class AzureWebPubSubSseService : ISseService
{
    private readonly WebPubSubServiceClient _client;

    public AzureWebPubSubSseService(WebPubSubServiceClient client)
    {
        _client = client;
    }

    public async Task CreateChannel(string userId, ISseResponseWriter writer, CancellationToken ct)
    {
        // In Azure Web PubSub, you don’t stream manually.
        // Instead, you return connection info so the client can connect directly.
        // To keep transparency, you proxy the SSE stream through your response writer.

        var url = _client.GetClientAccessUri(userId);

        writer.SetHeaders(new Dictionary<string,string> {
            ["Content-Type"] = "text/event-stream",
            ["Cache-Control"] = "no-cache",
            ["Connection"] = "keep-alive"
        });

        // Proxy the stream from Web PubSub back to the client
        using var http = new HttpClient();
        using var proxyResponse = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        using var stream = await proxyResponse.Content.ReadAsStreamAsync(ct);

        await stream.CopyToAsync(writer.GetStream(), ct);
    }

    public async void SendToUser(string userId, string message)
    {
        await _client.SendToUserAsync(userId, message);
    }

    public async void SendToUsers(HashSet<string> userIds, string message)
    {
        foreach (var id in userIds)
            await _client.SendToUserAsync(id, message);
    }
}

*/

/* Mercure Implementation

public class MercureForwardingSseService : ISseService
{
    private readonly HttpClient _httpClient;
    private readonly string _hubUrl;
    private readonly string _jwtToken;

    public MercureForwardingSseService(HttpClient httpClient, string hubUrl, string jwtToken)
    {
        _httpClient = httpClient;
        _hubUrl = hubUrl.TrimEnd('/');
        _jwtToken = jwtToken;
    }

    public async Task CreateChannel(string userId, ISseResponseWriter writer, CancellationToken ct)
    {
        // Build Mercure subscription URL for this user
        var subscribeUrl = $"{_hubUrl}/.well-known/mercure?topic={Uri.EscapeDataString($"user/{userId}")}";

        // Forward Mercure's SSE headers to the client
        writer.SetHeaders(new Dictionary<string,string> {
            ["Content-Type"] = "text/event-stream",
            ["Cache-Control"] = "no-cache",
            ["Connection"] = "keep-alive"
        });

        var request = new HttpRequestMessage(HttpMethod.Get, subscribeUrl);
        request.Headers.Add("Authorization", $"Bearer {_jwtToken}");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        using var stream = await response.Content.ReadAsStreamAsync(ct);

        await stream.CopyToAsync(writer.GetStream(), ct);
    }

    public async void SendToUser(string userId, string message)
    {
        var publishUrl = $"{_hubUrl}/.well-known/mercure";
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string,string>("topic", $"user/{userId}"),
            new KeyValuePair<string,string>("data", message)
        });

        var request = new HttpRequestMessage(HttpMethod.Post, publishUrl)
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {_jwtToken}");

        await _httpClient.SendAsync(request);
    }

    public void SendToUsers(HashSet<string> userIds, string message)
    {
        foreach (var id in userIds)
            SendToUser(id, message);
    }
}



*/