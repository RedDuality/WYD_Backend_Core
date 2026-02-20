using System.Security.Claims;
using System.Text.Json;
using Core.Model.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Core.Services.Util;

public class ContextService()
{
    public static string GetAccountId(ClaimsPrincipal? userPrincipal)
    {
        return userPrincipal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
            throw new UnauthorizedAccessException("No Account Id in the claims");
    }

    public static string GetEmail(ClaimsPrincipal? userPrincipal)
    {
        return userPrincipal?.FindFirstValue(ClaimTypes.Email) ??
            throw new UnauthorizedAccessException("No Email in the claims"); ;
    }

    public static string? TryGetUserId(ClaimsPrincipal? userPrincipal)
    {
        return userPrincipal?.FindFirstValue("userId");
    }

    public static string GetUserId(ClaimsPrincipal? userPrincipal)
    {
        return userPrincipal?.FindFirstValue("userId") ??
            throw new UnauthorizedAccessException("No User Id in the claims");
    }

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

    public static SignInType GetSignInProvider(ClaimsPrincipal? userPrincipal)
    {
        // 1. Find the 'firebase' claim
        var firebaseClaim = userPrincipal?.FindFirst("firebase")?.Value;

        if (string.IsNullOrEmpty(firebaseClaim))
        {
            throw new UnauthorizedAccessException("Firebase claim not found in the token.");
        }

        // 2. Parse the JSON to get 'sign_in_provider'
        using var jsonDoc = JsonDocument.Parse(firebaseClaim);
        if (jsonDoc.RootElement.TryGetProperty("sign_in_provider", out var providerElement))
        {

            var provider = providerElement.GetString();

            return provider switch
            {
                "password" => SignInType.Email,
                "google.com" => SignInType.Google,
                _ => throw new UnauthorizedAccessException($"Unsupported login method: {provider}")
            };
        }

        throw new UnauthorizedAccessException("Sign-in provider not found within Firebase claim.");
    }
}