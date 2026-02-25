using Core.Model.Users;

namespace Core.DTO.UserAPI;

public class AccountDto(Account account)
{
    public string Email { get; set; } = account.Email;

    public string SignInType { get; set; } = account.SignInType.ToString();

    public string? ImportedBy { get; set; } = account.ImportedByProfile.ToString();
}