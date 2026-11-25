using Microsoft.AspNetCore.Authorization;
using MiniErp.Application.Auth;

namespace MiniErp.Api.Auth;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionHandler(IIdentityService identity) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.AccountId() is not Guid id)
        {
            return;
        }

        var ct = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        var profile = await identity.GetProfileAsync(id, ct);
        if (profile?.Permissions.Contains(requirement.Permission, StringComparer.Ordinal) == true)
        {
            context.Succeed(requirement);
        }
    }
}
