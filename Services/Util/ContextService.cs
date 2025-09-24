using System.Security.Claims;
using Core.Model.Users;
using Core.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Core.Services.Util;

public class ContextService(UserService userService)
{
    public async Task<User> GetUser(ClaimsPrincipal? user)
    {
        // Retrieve the user ID from the claims
        string uid = user?.FindFirstValue(ClaimTypes.NameIdentifier) ??
            throw new UnauthorizedAccessException("No Id in the claims");

        string? email = user?.FindFirstValue(ClaimTypes.Email);

        return await userService.GetOrCreateAsync(uid, email);
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