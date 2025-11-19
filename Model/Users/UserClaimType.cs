namespace Core.Model.Users;

public enum UserClaimType
{
    // Profile
    CanViewProfileDetails,
    CanImpersonateProfile,
    CanEditProfile,
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