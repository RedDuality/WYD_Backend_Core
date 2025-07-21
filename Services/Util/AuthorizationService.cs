using Microsoft.AspNetCore.Http;
using Model;

namespace Service;
public class AuthorizationService(RequestService requestService, ProfileService profileService)
{
    private readonly RequestService _requestService = requestService;
    private readonly ProfileService _profileService = profileService;

    public async Task<User> VerifyRequest(HttpRequest req)
    {
        try
        {
            return await _requestService.VerifyRequestAsync(req);
        }
        catch (Exception)
        {
            throw new UnauthorizedAccessException();
        }
    }

    public async Task<Profile> VerifyRequest(HttpRequest req, UserPermissionOnProfile permissionOnProfile)
    {
        try
        {
            await _requestService.VerifyRequestAsync(req);
        }
        catch (Exception)
        {
            throw new UnauthorizedAccessException();
        }
        //TODO check roles
        string profileHash = RequestService.RetrieveFromHeaders(req,"Current-Profile");
        return _profileService.RetrieveByHash(profileHash);
    }

}