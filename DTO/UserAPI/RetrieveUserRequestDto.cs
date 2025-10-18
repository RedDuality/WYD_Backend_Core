using Core.DTO.ProfileAPI;
using Core.Model.Profiles;
using Core.Model.Users;

namespace Core.DTO.UserAPI;

public class RetrieveUserResponseDto(User user, List<Tuple<Profile, UserProfile>> profiles)
{
    public string? Hash { get; set; } = user.Id.ToString();
    //public List<AccountDto> Accounts { get; set; } = user.Accounts.Select(account => new AccountDto(account)).ToList();
    public List<RetrieveProfileResponseDto> Profiles { get; set; } = profiles.Select(up => new RetrieveProfileResponseDto(up.Item1, up.Item2)).ToList();
}