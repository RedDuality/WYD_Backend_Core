using Microsoft.AspNetCore.Http;

namespace Core.Components.ServerSentMessages;

public class AspNetSseResponseWriter(HttpResponse response) : ISseResponseWriter
{
    private readonly HttpResponse _response = response;

    public void SetHeaders(IDictionary<string, string> headers)
    {
        foreach (var kv in headers)
            _response.Headers[kv.Key] = kv.Value;
    }

    public Task WriteAsync(string payload, CancellationToken cancellationToken) =>
        _response.WriteAsync(payload, cancellationToken);

    public Task FlushAsync(CancellationToken cancellationToken) =>
        _response.Body.FlushAsync(cancellationToken);

    //public Stream GetStream() => _response.Body;
}
