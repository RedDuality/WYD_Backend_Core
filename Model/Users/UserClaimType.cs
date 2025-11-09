namespace Core.Model.Users;

public enum UserClaimType
{
    // Profile
    CanViewProfileDetails,
    CanImpersonateProfile,
    // Community
    CanViewCommunity,
    CanCreateCommunity,
    CanEditCommunity,
    // Events
    CanReadEvents,
    CanCreateEvents,
    CanEditEvents,
    CanShareEvents,

}