namespace Core.External.Interfaces;

public interface IAuthenticationService
{
    public Task<string> RetrieveMail(string uid);
}