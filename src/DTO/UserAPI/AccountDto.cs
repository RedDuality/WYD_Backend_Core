using Core.Model.Users;

namespace Core.DTO.UserAPI;

public class AccountDto(Account account)
{
    public string Mail { get; set; } = account.Email;
    public string Uid { get; set; } = account.Uid;
}