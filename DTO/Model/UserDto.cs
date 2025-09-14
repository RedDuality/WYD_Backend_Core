using Core.Model;
using Core.Model.Join;

namespace Core.DTO.Model;

public class UserDto(User user, List<Tuple<Profile, UserProfile>> profiles)
{
    public string? Hash { get; set; } = user.Id.ToString();
    //public List<AccountDto> Accounts { get; set; } = user.Accounts.Select(account => new AccountDto(account)).ToList();
    public List<UserProfileDto> Profiles { get; set; } = profiles.Select(up => new UserProfileDto(up.Item1, up.Item2)).ToList();
}