using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using System.Text;
using Core.External.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Core.External.Authentication;

public class FirebaseAuthService : IAuthenticationService
{
    private readonly Lazy<FirebaseAuth> _authInstance;

    public FirebaseAuthService(IConfiguration configuration)
    {
        _authInstance = new Lazy<FirebaseAuth>(() =>
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                var projectId = configuration.GetValue<string>("AUTHENTICATION_AUDIENCE") ??
                    throw new InvalidOperationException("Firebase Project Id(audience) not found between environment variables");
                var json = configuration.GetValue<string>("AUTHENTICATION_CREDENTIALS") ??
                    throw new InvalidOperationException("Google credentials not found between environment variables");

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var credential = GoogleCredential.FromStream(stream);

                FirebaseApp.Create(new AppOptions
                {
                    Credential = credential,
                    ProjectId = projectId
                });
            }

            return FirebaseAuth.DefaultInstance;
        });
    }

    public FirebaseAuth GetInstance() => _authInstance.Value;

    /// <summary>
    /// Safely adds or updates the "userId" custom claim for a Firebase user.
    /// </summary>
    /// <param name="firebaseUid">The Firebase UID of the user.</param>
    /// <param name="userId">Your internal userId to attach as a claim.</param>
    public async Task AddOrUpdateUserIdClaimAsync(string firebaseUid, string userId)
    {
        var auth = GetInstance();

        var user = await auth.GetUserAsync(firebaseUid);
        var currentClaims = user.CustomClaims ?? new Dictionary<string, object>();

        var updatedClaims = new Dictionary<string, object>(currentClaims)
        {
            ["userId"] = userId
        };

        await auth.SetCustomUserClaimsAsync(firebaseUid, updatedClaims);
    }

}