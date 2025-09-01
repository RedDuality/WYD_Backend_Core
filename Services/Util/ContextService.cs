using System.Security.Claims;
using Core.Model;
using Core.Services.Model;

namespace Core.Services.Util;

public class ContextService(UserService userService)
{
    public async Task<User> GetUser(ClaimsPrincipal? user)
    {
        // Retrieve the user ID from the claims
        string uid = user?.FindFirstValue(ClaimTypes.NameIdentifier) ??
            throw new UnauthorizedAccessException("No Id in the claims");

        return await userService.GetOrCreateAsync(uid);
    }
}