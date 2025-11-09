using System.Security.Claims;
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
}