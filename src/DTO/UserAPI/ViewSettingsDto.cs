using Core.Model.Users;

namespace Core.DTO.UserAPI;

public class ViewSettingsDto(ViewSettings settings)
{
    public string ProfileId { get; set; } = settings.ProfileId.ToString();
    public bool ViewConfirmed { get; set; } = settings.ViewConfirmed;
    public bool ViewShared { get; set; } = settings.ViewShared;
}