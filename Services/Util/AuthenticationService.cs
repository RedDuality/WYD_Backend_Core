using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Service;
public class AuthenticationService() : IAuthenticationService
{

    private static FirebaseAuth GetInstance()
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            var googleCredentialsJson = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS");

            if (string.IsNullOrEmpty(googleCredentialsJson))
            {
                throw new Exception("Google credentials not found in the environment variable");
            }

            GoogleCredential credential;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(googleCredentialsJson)))
            {
                credential = GoogleCredential.FromStream(stream);
            }

            FirebaseApp.Create(new AppOptions
            {
                Credential = credential,
                ProjectId = "wydaccounts",
            });
        }
        return FirebaseAuth.DefaultInstance;
    }

    public async Task<string> CheckTokenAsync(string token)
    {
        FirebaseToken decodedToken = await GetInstance().VerifyIdTokenAsync(token);
        string uid = decodedToken.Uid;
        return uid;
    }

    public async Task<UserRecord> RetrieveAccount(string uid)
    {
        try
        {
            var userRecord = await GetInstance().GetUserAsync(uid);
            return GetUserRecord(userRecord);
        }
        catch (Exception)
        {
            throw new SecurityTokenValidationException("No Firebase user found");
        }

    }
    //Util
    public static async Task<UserRecord> CreateAccountAsync(UserRecordArgs userRecordArgs)
    {
        var userRecord = await GetInstance().CreateUserAsync(userRecordArgs);
        return GetUserRecord(userRecord);
    }

    public static async Task<UserRecord> RetrieveAccountFromMail(string mail)
    {
        var userRecord = await GetInstance().GetUserByEmailAsync(mail);
        return GetUserRecord(userRecord);
    }

    private static UserRecord GetUserRecord(FirebaseAdmin.Auth.UserRecord userRecord)
    {
        return new UserRecord(userRecord.Email, userRecord.Uid);
    }

}