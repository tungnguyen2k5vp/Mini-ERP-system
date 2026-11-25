using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Domain;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Auth;

public sealed class IdentityService(ErpDbContext db, UserManager<ErpUser> users, SignInManager<ErpUser> signIn,
    ProfileReader profiles, TimeProvider clock) : IIdentityService
{
    public async Task<IssuedSession?> LoginAsync(LoginRequest request, RequestActor actor, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        var now = clock.GetUtcNow();
        var result = user is { IsDisabled: false }
            ? await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)
            : SignInResult.Failed;
        if (!result.Succeeded || user is null)
        {
            db.AuditEntries.Add(AuditEntry.Create(actor with { UserId = user?.Id }, now.UtcDateTime,
                "Login", "User", user?.Id.ToString(), "Denied"));
            await db.SaveChangesAsync(ct);
            return null;
        }

        var rawToken = NewToken();
        var session = new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(rawToken),
            SecurityStamp = user.SecurityStamp!,
            ExpiresAtUtc = now.AddDays(7).UtcDateTime
        };
        db.RefreshSessions.Add(session);
        db.AuditEntries.Add(AuditEntry.Create(actor with { UserId = user.Id }, now.UtcDateTime, "Login", "User",
            user.Id.ToString()));
        await db.SaveChangesAsync(ct);
        return await IssueAsync(session, rawToken, ct);
    }

    public async Task<IssuedSession?> RefreshAsync(string rawToken, CancellationToken ct)
    {
        if (rawToken.Length != 64)
        {
            return null;
        }

        var hash = Hash(rawToken);
        var session = await ActiveSessions(clock.GetUtcNow().UtcDateTime)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (session is null)
        {
            return null;
        }

        var replacement = NewToken();
        session.TokenHash = Hash(replacement);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }

        return await IssueAsync(session, replacement, ct);
    }

    public async Task LogoutAsync(string? rawToken, Guid? sessionId, RequestActor actor, CancellationToken ct)
    {
        var hash = rawToken is { Length: 64 } ? Hash(rawToken) : null;
        if (hash is null && sessionId is null)
        {
            return;
        }

        var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.RevokedAtUtc == null &&
            ((sessionId != null && x.Id == sessionId) ||
             (sessionId == null && hash != null && x.TokenHash == hash)), ct);
        if (session is null)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.RefreshSessions.Where(x => x.Id == session.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAtUtc, now), ct);
        if (updated == 1)
        {
            db.AuditEntries.Add(AuditEntry.Create(actor with { UserId = session.UserId }, now, "Logout",
                "User", session.UserId.ToString()));
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    public Task<UserProfile?> GetProfileAsync(Guid id, CancellationToken ct) => profiles.GetAsync(id, ct);

    public Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, CancellationToken ct) =>
        ActiveSessions(clock.GetUtcNow().UtcDateTime).AsNoTracking()
            .AnyAsync(x => x.Id == sessionId && x.UserId == userId, ct);

    public async Task ChangePasswordAsync(Guid id, ChangePasswordRequest request, RequestActor actor, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString()) ??
            throw ErpException.Missing("user_not_found", "Tài khoản không tồn tại.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        IdentityResults.Ensure(await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword));
        db.AuditEntries.Add(AuditEntry.Create(actor, clock.GetUtcNow().UtcDateTime, "ChangePassword", "User", id.ToString()));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private IQueryable<RefreshSession> ActiveSessions(DateTime now) => db.RefreshSessions.Where(x =>
        x.RevokedAtUtc == null && x.ExpiresAtUtc > now && !x.User.IsDisabled && x.User.SecurityStamp == x.SecurityStamp &&
        (!x.User.LockoutEnabled || x.User.LockoutEnd == null || x.User.LockoutEnd <= new DateTimeOffset(now)));

    private async Task<IssuedSession> IssueAsync(RefreshSession session, string rawToken, CancellationToken ct) =>
        new(session.Id, rawToken, new DateTimeOffset(session.ExpiresAtUtc, TimeSpan.Zero),
            (await profiles.GetAsync(session.UserId, ct))!);

    private static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal static class IdentityResults
{
    public static void Ensure(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var codes = result.Errors.Select(error => error.Code).ToArray();
        if (codes.Contains("ConcurrencyFailure"))
        {
            throw ErpException.Conflict("stale_version", "Dữ liệu đã thay đổi. Tải lại trước khi thử lại.");
        }
        if (codes.Any(code => code is "DuplicateUserName" or "DuplicateEmail"))
        {
            throw ErpException.Conflict("duplicate_email", "Email này đã được sử dụng.");
        }
        if (codes.Contains("PasswordMismatch"))
        {
            throw ErpException.Invalid("invalid_password", "Mật khẩu hiện tại chưa đúng.");
        }
        if (codes.Any(code => code.StartsWith("Password", StringComparison.Ordinal)))
        {
            throw ErpException.Invalid("password_policy",
                "Mật khẩu cần ít nhất 12 ký tự, có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.");
        }

        throw ErpException.Invalid("invalid_account", "Thông tin tài khoản không hợp lệ.");
    }
}
