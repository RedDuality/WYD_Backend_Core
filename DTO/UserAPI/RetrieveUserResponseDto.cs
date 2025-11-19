using Core.Model.Profiles;
using Core.Model.Users;

namespace Core.DTO.UserAPI;

public class RetrieveUserResponseDto(User user, List<Tuple<Profile, UserProfile, UserClaims>> profiles)
{
    public string Id { get; set; } = user.Id.ToString();
    public string MainProfileId { get; set; } = user.MainProfileId.ToString();
    public HashSet<string> ProfileIds { get; set; } = [.. user.ProfileIds.Select((id) => id.ToString())];
    //public List<AccountDto> Accounts { get; set; } = user.Accounts.Select(account => new AccountDto(account)).ToList();
    public List<RetrieveDetailedProfileResponseDto> Profiles { get; set; } =
        [.. profiles.Select(up => new RetrieveDetailedProfileResponseDto(up.Item1, up.Item2, up.Item3))];
}