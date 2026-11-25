using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Domain;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Auth;

public sealed class IdentityAdministration(ErpDbContext db, UserManager<ErpUser> users, TimeProvider clock)
    : IIdentityAdministration
{
    public async Task<Page<UserAccount>> ListUsersAsync(string? search, int page, int size, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Email!.Contains(term) || x.DisplayName.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var now = clock.GetUtcNow();
        var rows = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new UserAccount(x.Id, x.Email!, x.DisplayName,
                (from membership in db.UserRoles
                 join role in db.Roles on membership.RoleId equals role.Id
                 where membership.UserId == x.Id
                 orderby role.Name
                 select role.Name).FirstOrDefault() ?? "",
                x.IsDisabled, x.LockoutEnabled && x.LockoutEnd > now, x.ConcurrencyStamp!))
            .ToListAsync(ct);
        return new Page<UserAccount>(rows, page, size, total);
    }

    public async Task<UserAccount> CreateUserAsync(CreateUserRequest request, RequestActor actor, CancellationToken ct)
    {
        EnsureRole(request.Role);
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new ErpUser
        {
            Id = Guid.NewGuid(), Email = request.Email.Trim(), UserName = request.Email.Trim(),
            DisplayName = request.DisplayName.Trim(), CreatedAtUtc = now
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        IdentityResults.Ensure(await users.CreateAsync(user, request.Password));
        IdentityResults.Ensure(await users.AddToRoleAsync(user, request.Role));
        db.AuditEntries.Add(AuditEntry.Create(actor, now, "CreateUser", "User", user.Id.ToString(), after: request.Role));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToAccount(user, request.Role);
    }

    public async Task<UserAccount> UpdateUserAsync(Guid id, UpdateUserRequest request, RequestActor actor, CancellationToken ct)
    {
        EnsureRole(request.Role);
        var user = await users.FindByIdAsync(id.ToString()) ??
            throw ErpException.Missing("user_not_found", "Tài khoản không tồn tại.");
        if (user.ConcurrencyStamp != request.Version)
        {
            throw ErpException.Conflict("stale_version", "Tài khoản đã thay đổi. Tải lại danh sách.");
        }

        var previous = await users.GetRolesAsync(user);
        if (previous.Contains(Permissions.Administrator) || id == actor.UserId)
        {
            throw ErpException.Conflict("protected_account", "Không thể khóa hoặc đổi vai trò tài khoản quản trị.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var before = $"Role={string.Join(',', previous)}; Disabled={user.IsDisabled}";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        user.IsDisabled = request.IsDisabled;
        if (!request.IsDisabled)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
        }

        IdentityResults.Ensure(await users.UpdateSecurityStampAsync(user));
        if (previous.Count != 1 || previous[0] != request.Role)
        {
            if (previous.Count > 0)
            {
                IdentityResults.Ensure(await users.RemoveFromRolesAsync(user, previous));
            }
            IdentityResults.Ensure(await users.AddToRoleAsync(user, request.Role));
        }

        db.AuditEntries.Add(AuditEntry.Create(actor, now, "UpdateUserAccess", "User", user.Id.ToString(),
            before: before, after: $"Role={request.Role}; Disabled={user.IsDisabled}"));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToAccount(user, request.Role);
    }

    public async Task<RoleAccess[]> ListRolesAsync(CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var grants = await db.RoleClaims.Where(x => x.ClaimType == Permissions.ClaimType)
            .Select(x => new { x.RoleId, x.ClaimValue }).ToListAsync(ct);
        var byRole = grants.ToLookup(x => x.RoleId, x => x.ClaimValue!);
        return roles.Select(x => ToRole(x, byRole[x.Id].Order(StringComparer.Ordinal).ToArray())).ToArray();
    }

    public async Task<RoleAccess> UpdateRoleAsync(string name, UpdateRoleRequest request, RequestActor actor, CancellationToken ct)
    {
        if (string.Equals(name, Permissions.Administrator, StringComparison.OrdinalIgnoreCase))
        {
            throw ErpException.Conflict("protected_role", "Vai trò quản trị giữ toàn quyền hệ thống.");
        }

        if (request.Permissions.Any(x => !Permissions.All.Contains(x) ||
                x is Permissions.ManageUsers or Permissions.ManageRoles))
        {
            throw ErpException.Invalid("invalid_permissions", "Danh sách có quyền không được phép gán cho vai trò.");
        }

        var role = await db.Roles.SingleOrDefaultAsync(x => x.Name == name, ct) ??
            throw ErpException.Missing("role_not_found", "Vai trò không tồn tại.");
        if (role.ConcurrencyStamp != request.Version)
        {
            throw ErpException.Conflict("stale_version", "Vai trò đã thay đổi. Tải lại bảng phân quyền.");
        }

        var existing = await db.RoleClaims.Where(x => x.RoleId == role.Id && x.ClaimType == Permissions.ClaimType).ToListAsync(ct);
        var next = request.Permissions.Distinct().Order(StringComparer.Ordinal).ToArray();
        db.RoleClaims.RemoveRange(existing);
        db.RoleClaims.AddRange(next.Select(permission => new IdentityRoleClaim<Guid>
        {
            RoleId = role.Id, ClaimType = Permissions.ClaimType, ClaimValue = permission
        }));
        role.ConcurrencyStamp = Guid.NewGuid().ToString();
        db.AuditEntries.Add(AuditEntry.Create(actor, clock.GetUtcNow().UtcDateTime, "UpdateRoleGrants", "Role",
            role.Id.ToString(), before: string.Join(',', existing.Select(x => x.ClaimValue)), after: string.Join(',', next)));
        await db.SaveChangesAsync(ct);
        return ToRole(role, next);
    }

    private UserAccount ToAccount(ErpUser user, string role) => new(user.Id, user.Email!, user.DisplayName, role,
        user.IsDisabled, user.LockoutEnabled && user.LockoutEnd > clock.GetUtcNow(), user.ConcurrencyStamp!);

    private static RoleAccess ToRole(IdentityRole<Guid> role, string[] grants) =>
        new(role.Name!, role.Name == Permissions.Administrator ? [.. Permissions.All] : grants, role.ConcurrencyStamp!);

    private static void EnsureRole(string role)
    {
        if (!Permissions.DefaultRoles.ContainsKey(role))
        {
            throw ErpException.Invalid("invalid_role", "Vai trò không hợp lệ.");
        }
    }
}

public sealed class IdentityProvisioner(ErpDbContext db, RoleManager<IdentityRole<Guid>> roles, UserManager<ErpUser> users,
    IIdentityAdministration administration)
{
    public async Task EnsureRolesAsync(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var (name, permissions) in Permissions.DefaultRoles)
        {
            if (await roles.RoleExistsAsync(name))
            {
                continue;
            }

            var role = new IdentityRole<Guid>(name) { Id = Guid.NewGuid() };
            IdentityResults.Ensure(await roles.CreateAsync(role));
            foreach (var permission in permissions)
            {
                IdentityResults.Ensure(await roles.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission)));
            }
        }

        await transaction.CommitAsync(ct);
    }

    public async Task EnsureAdministratorAsync(string email, string password, CancellationToken ct)
    {
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null && await users.IsInRoleAsync(existing, Permissions.Administrator))
        {
            return;
        }

        if ((await users.GetUsersInRoleAsync(Permissions.Administrator)).Count > 0)
        {
            throw new InvalidOperationException("An administrator already exists. Use the user management screen.");
        }

        await administration.CreateUserAsync(new CreateUserRequest(email, "Quản trị hệ thống", password,
            Permissions.Administrator), new RequestActor(null, "provision-administrator"), ct);
    }
}
