using Core.Model;
using Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Service;


namespace Core.Services.Util;

public class TokenService(
    UserService userService,
    IAuthenticationService authenticationService
)
{

    public async Task<User> VerifyRequestAsync(HttpRequest req)
    {
        var authorization = req.Headers.Authorization;

        if (string.IsNullOrEmpty(authorization) || !authorization.ToString().StartsWith("Bearer "))
        {
            throw new ArgumentNullException(
                "No token in the request, or token not in the right format"
            );
        }

        string token = authorization.ToString()["Bearer ".Length..].Trim();

        return await CheckTokenAsync(token);
    }

    public async Task<User> CheckTokenAsync(string token)
    {
        string uid;
        try
        {
            uid = await authenticationService.CheckTokenAsync(token);
        }
        catch (Exception)
        {
            throw new SecurityTokenValidationException("Invalid Token");
        }
        return await userService.GetOrCreateAsync(uid);
    }

}