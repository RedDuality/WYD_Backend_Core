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

}