namespace Core.Services.Util;

public interface IContextManager
{
    string GetAccountId();

    string GetEmail();

    string GetUserId();

    string GetCurrentProfileId();
}

