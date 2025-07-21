
namespace Service;

public class UserRecord (string email, string uid){
    public string Email {get;} = email ;
    public string Uid {get;} = uid;
}


public interface IAuthenticationService
{
    public Task<string> CheckTokenAsync(string token);

    public Task<UserRecord> RetrieveAccount(string uid);
}