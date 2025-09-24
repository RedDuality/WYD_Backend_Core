using Core.Model.Profile;
using Core.Model.Users;

namespace Core.DTO.UserAPI;

public class RetrieveUserRequestDto(User user, List<Tuple<Profile, UserProfile>> profiles)
{
    public string? Hash { get; set; } = user.Id.ToString();
    //public List<AccountDto> Accounts { get; set; } = user.Accounts.Select(account => new AccountDto(account)).ToList();
    public List<RetrieveUserProfileResponseDto> Profiles { get; set; } = profiles.Select(up => new RetrieveUserProfileResponseDto(up.Item1, up.Item2)).ToList();
}