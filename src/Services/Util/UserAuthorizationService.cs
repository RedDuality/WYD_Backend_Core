using Core.Model.Users;
using Core.Services.Users;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Bson;

namespace Core.Services.Util;

public class UserAuthorizationService(UserClaimService claimService, IContextManager contextManager) : AuthorizationHandler<UserClaimRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        UserClaimRequirement requirement)
    {
        ObjectId userId = new(contextManager.GetUserId());
        ObjectId profileId = new(contextManager.GetCurrentProfileId());

        var hasPermit = await claimService.CheckHasPermit(userId, profileId, requirement.ClaimType);

        if (hasPermit)
        {
            context.Succeed(requirement);
        }
        else
        {
            throw new UnauthorizedAccessException(
                $"User {userId} does not have '{requirement.ClaimType}' permission on profile {profileId}."
            );
        }
    }
}



public class UserClaimRequirement(UserClaimType claimType) : IAuthorizationRequirement
{
    public UserClaimType ClaimType { get; } = claimType;
}
