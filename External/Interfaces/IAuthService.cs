namespace Core.External.Interfaces;

public interface IAuthService
{
    Task AddOrUpdateClaimsAsync(string loginServiceUid, Dictionary<string, string> claimsToUpdate);
}