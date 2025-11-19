using System.Collections.ObjectModel;

namespace Core.Model.Users;

public enum PresetClaimRole
{
    Admin,
    Viewer,
}

public static class PresetClaimRoleMapper
{
    private static readonly ReadOnlyDictionary<PresetClaimRole, IReadOnlyList<UserClaimType>> RoleClaimMap =
        new(
            new Dictionary<PresetClaimRole, IReadOnlyList<UserClaimType>>
            {
                {
                    PresetClaimRole.Admin, new List<UserClaimType>
                    {
                        UserClaimType.CanViewProfileDetails,
                        UserClaimType.CanImpersonateProfile,
                        UserClaimType.CanEditProfile,
                        UserClaimType.CanViewCommunity,
                        UserClaimType.CanCreateCommunity,
                        UserClaimType.CanEditCommunity,
                        UserClaimType.CanReadEvents,
                        UserClaimType.CanCreateEvents,
                        UserClaimType.CanEditEvents,
                        UserClaimType.CanShareEvents,
                    }.AsReadOnly()
                },
                {
                    PresetClaimRole.Viewer, new List<UserClaimType>
                    {
                        UserClaimType.CanViewProfileDetails,
                        UserClaimType.CanReadEvents
                    }.AsReadOnly()
                }
            });

    /// <summary>
    /// Retrieves the list of claims associated with a specific PresetClaimRole.
    /// </summary>
    public static IReadOnlyList<UserClaimType> GetClaimsForRole(PresetClaimRole role)
    {
        if (RoleClaimMap.TryGetValue(role, out var claims))
        {
            return claims;
        }

        return new List<UserClaimType>().AsReadOnly();
    }
}