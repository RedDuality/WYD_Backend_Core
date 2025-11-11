namespace Core.Services.Util;

public interface IContextManager
{
    string GetAccountId();

    string GetEmail();

    string? TryGetUserId();

    string GetUserId();

    string GetCurrentProfileId();
}

