namespace Core.Services.Interfaces;

public class UserLoginRecord (string email, string uid){
    public string Email {get;} = email ;
    public string Uid {get;} = uid;
}


public interface IAuthenticationService
{
    public Task<string> CheckTokenAsync(string token);

    public Task<UserLoginRecord> RetrieveAccount(string uid);
}