using Core.Services.Users;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;

namespace Core.Services.Notifications;

public class NotificationService
{

    private readonly Lazy<FirebaseMessaging> _messagingInstance;
    private readonly DeviceService _deviceService;


    public NotificationService(IConfiguration configuration, DeviceService deviceService)
    {
        _deviceService = deviceService;
        _messagingInstance = new Lazy<FirebaseMessaging>(() =>
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                var googleCredentials = configuration["GOOGLE_APPLICATION_CREDENTIALS"]
                    ?? throw new InvalidOperationException("'GOOGLE_APPLICATION_CREDENTIALS' is not set in configuration.");

                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromJson(googleCredentials)
                });
            }

            return FirebaseMessaging.DefaultInstance;
        }, isThreadSafe: true);
    }

    private FirebaseMessaging Messaging => _messagingInstance.Value;

    public async Task SendNotification(Dictionary<string, ObjectId> tokensWithUserIds, Dictionary<string, string>? data = null)
    {
        var tokens = tokensWithUserIds.Keys.ToList();

        var message = new MulticastMessage()
        {
            Notification = null,
            Tokens = tokens,
            Data = data,
        };

        BatchResponse response = await Messaging.SendEachForMulticastAsync(message);

        await HandleFailedNotifications(response, tokensWithUserIds); 
    }

    private async Task HandleFailedNotifications(BatchResponse response, Dictionary<string, ObjectId> tokensWithUserIds)
    {
        for (int i = 0; i < response.Responses.Count; i++)
        {
            var fcmResponse = response.Responses[i];

            if (!fcmResponse.IsSuccess)
            {
                var failedToken = tokensWithUserIds.Keys.ElementAt(i);
                var userId = tokensWithUserIds[failedToken];

                if (IsTokenInvalidOrExpired(fcmResponse))
                {
                    await _deviceService.RemoveDevice(userId, failedToken);
                }
                else
                {
                    // This is a transient error (e.g., server unavailable, resource exhausted).
                    Console.WriteLine($"[FCM TRANSIENT] Notification failed for token '{failedToken}' (User: {userId}). Error: {fcmResponse.Exception?.Message}. Token retained for retry.");
                }
            }
        }
    }

    /// <summary>
    /// Checks the response exception to determine if the error is permanent (token should be deleted).
    /// </summary>
    /// <param name="response">The SendResponse from the batch operation.</param>
    /// <returns>True if the token is permanently invalid or expired.</returns>
    private static bool IsTokenInvalidOrExpired(SendResponse response)
    {
        if (response.Exception is null)
        {
            return false;
        }

        // Look for the inner FirebaseMessagingException
        var innerException = response.Exception.InnerException;
        if (innerException is null)
        {
            return false;
        }

        // 1. Check for the specific 'messaging/not-registered' message/code.
        // This is the most common reason to delete a token.
        if (innerException.Message.Contains("messaging/not-registered") ||
            innerException.Message.Contains("messaging/invalid-argument"))
        {
            return true;
        }

        // 2. Check for specific status codes indicating permanent failure.
        // This usually requires casting to a Google.Apis.Requests.RequestError,
        // though the Firebase Admin SDK often wraps it. We'll look for specific HTTP status codes
        // that indicate a non-recoverable client-side error (HTTP 400).
        if (innerException is FirebaseMessagingException fcmEx)
        {
            // The FirebaseMessagingException often contains the detailed error.
            // Check for specific error codes defined by Firebase/GCP.
            if (fcmEx.MessagingErrorCode.HasValue)
            {
                var errorCode = fcmEx.MessagingErrorCode.Value.ToString();

                // Codes indicating the token is bad and should be removed:
                if (errorCode == "UNREGISTERED" || // The token is no longer valid.
                    errorCode == "INVALID_ARGUMENT" || // Invalid token format or size.
                    errorCode == "SENDER_ID_MISMATCH") // Token belongs to another project.
                {
                    return true;
                }
            }
        }

        // 3. Fallback check for HTTP 400 status (Bad Request/Permanent client-side issue)
        // This is less common but serves as a generic safety net for bad tokens.
        if (innerException is GoogleApiException googleEx && googleEx.HttpStatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            return true;
        }

        return false;
    }
}
