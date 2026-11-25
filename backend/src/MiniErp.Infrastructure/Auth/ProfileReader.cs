using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Auth;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Auth;

public sealed class ProfileReader(ErpDbContext db)
{
    public async Task<UserProfile?> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.Id == userId)
            .Select(x => new { x.Id, x.Email, x.DisplayName }).SingleOrDefaultAsync(ct);
        if (user is null)
        {
            return null;
        }

        var memberships = from membership in db.UserRoles
                          join role in db.Roles on membership.RoleId equals role.Id
                          where membership.UserId == userId
                          select new { role.Id, role.Name };
        var roles = await memberships.Select(x => x.Name!).OrderBy(x => x).ToArrayAsync(ct);
        if (roles.Contains(Permissions.Administrator, StringComparer.Ordinal))
        {
            return new UserProfile(user.Id, user.Email!, user.DisplayName, roles, [.. Permissions.All]);
        }

        var grants = await db.RoleClaims.Where(x => x.ClaimType == Permissions.ClaimType &&
                memberships.Any(role => role.Id == x.RoleId))
            .Select(x => x.ClaimValue!).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        return new UserProfile(user.Id, user.Email!, user.DisplayName, roles, grants);
    }
}
