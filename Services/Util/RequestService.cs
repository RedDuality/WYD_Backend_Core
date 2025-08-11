//using AutoMapper.Configuration.Annotations;
//using Azure;
//using Microsoft.Extensions.Primitives;


namespace Core.Services.Util;

public class RequestService(
    //JsonSerializerOptions jsonSerializerOptions,
    //UserService userService,
    //IAuthenticationService authenticationService,
    //NotificationService notificationService
)
{

    /*
    private readonly JsonSerializerOptions _jsonSerializerOptions = jsonSerializerOptions;

    private readonly UserService _userService = userService;

    private readonly IAuthenticationService _authenticationService = authenticationService;

    private readonly NotificationService notificationService = notificationService;

    public static string RetrieveFromHeaders(HttpRequest req, string headerKey)
    {
        if (req.Headers.TryGetValue(headerKey, out var headerValue))
        {
            if (StringValues.IsNullOrEmpty(headerValue))
            {
                throw new ArgumentException("Header value malformed");
            }
            return headerValue!;
        }
        else
            throw new ArgumentException(headerKey + " header not found or in the wrong format");
    }

    public async Task<T> DeserializeRequestBodyAsync<T>(HttpRequest req)
    {
        string requestBody;
        using (StreamReader reader = new(req.Body, Encoding.UTF8))
        {
            requestBody = await reader.ReadToEndAsync();
        }
        return JsonSerializer.Deserialize<T>(requestBody, _jsonSerializerOptions)
            ?? throw new ArgumentNullException(nameof(T));
    }

    public async Task NotifyAsync(Event ev, UpdateType type, Profile currentProfile)
    {
        try
        {
            //TODO check max time
            await notificationService.SendEventNotifications(ev, currentProfile, type);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public async Task NotifyAsync(Event? ev, UpdateType type, Profile currentProfile, string hash)
    {
        try
        {
            //TODO check max time
            await notificationService.SendEventNotifications(ev, currentProfile, type, hash);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
*/
}