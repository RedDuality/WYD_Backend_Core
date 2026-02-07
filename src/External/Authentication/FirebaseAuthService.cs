

using Core.External.Interfaces;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;

namespace Core.External.Authentication;

public class FirebaseAuthService : IAuthService
{
    private readonly Lazy<FirebaseAuth> _authInstance;

    public FirebaseAuthService(IConfiguration configuration)
    {
        _authInstance = new Lazy<FirebaseAuth>(() =>
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                var googleCredentials = configuration.GetValue<string>("GOOGLE_APPLICATION_CREDENTIALS") ??
                    throw new InvalidOperationException("Google credentials not found between environment variables");

                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromJson(googleCredentials)
                });
            }

            return FirebaseAuth.DefaultInstance;
        });
    }

    public FirebaseAuth GetInstance() => _authInstance.Value;

    /// <summary>
    /// Safely adds or updates multiple custom claims for a Firebase user.
    /// This method preserves any existing claims not present in claimsToUpdate.
    /// </summary>
    /// <param name="firebaseUid">The Firebase UID of the user.</param>
    /// <param name="claimsToUpdate">A dictionary of string-string key/value pairs representing the claims to set or update.</param>
    public async Task AddOrUpdateClaimsAsync(string firebaseUid, Dictionary<string, string> claimsToUpdate)
    {
        var auth = GetInstance();
        try
        {
            var user = await auth.GetUserAsync(firebaseUid);

            var updatedClaims = user.CustomClaims != null
                ? new Dictionary<string, object>(user.CustomClaims)
                : [];

            foreach (var claim in claimsToUpdate)
            {
                updatedClaims[claim.Key] = claim.Value;
            }

            await auth.SetCustomUserClaimsAsync(firebaseUid, updatedClaims);
        }
        catch (FirebaseAdmin.Auth.FirebaseAuthException ex)
        {
            Console.WriteLine($"Error updating claims for UID {firebaseUid}: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            throw;
        }
    }
}