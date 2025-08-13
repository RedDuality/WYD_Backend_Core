namespace Core.Model.Dto;

public class AccountDto(Account account)
{
    public string Mail { get; set; } = account.Email;
    public string Uid { get; set; } = account.Uid;
}