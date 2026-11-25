using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MiniErp.Application;
using MiniErp.Application.Auth;

namespace MiniErp.Api.Auth;

public sealed class AuthSettings
{
    public const string Issuer = "MiniErp";
    public const string Audience = "MiniErp.Api";
    public string SigningKey { get; init; } = "";
    public string PublicOrigin { get; init; } = "";

    public static bool HasValidKey(AuthSettings settings)
    {
        Span<byte> bytes = stackalloc byte[128];
        return Convert.TryFromBase64String(settings.SigningKey, bytes, out var length) && length >= 32;
    }

    public bool HasValidOrigin(bool development) =>
        Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var uri) &&
        uri.GetLeftPart(UriPartial.Authority) == PublicOrigin && string.IsNullOrEmpty(uri.UserInfo) &&
        (uri.Scheme == "https" || (development && uri.IsLoopback && uri.Scheme == "http"));

    public SymmetricSecurityKey Key() => new(Convert.FromBase64String(SigningKey));
}

/// <summary>Short-lived bearer token and the current user profile. The refresh credential is set by cookie.</summary>
public sealed record SessionResponse(string AccessToken, DateTimeOffset ExpiresAt, UserProfile User);

public sealed class AccessTokenIssuer(IOptions<AuthSettings> settings, TimeProvider clock)
{
    public SessionResponse Issue(IssuedSession session)
    {
        var now = clock.GetUtcNow();
        var expiry = now.AddMinutes(10);
        var token = new JwtSecurityToken(AuthSettings.Issuer, AuthSettings.Audience,
        [
            new Claim(JwtRegisteredClaimNames.Sub, session.User.Id.ToString()),
            new Claim("sid", session.SessionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        ], now.UtcDateTime, expiry.UtcDateTime, new SigningCredentials(settings.Value.Key(), SecurityAlgorithms.HmacSha256));
        return new SessionResponse(new JwtSecurityTokenHandler().WriteToken(token), expiry, session.User);
    }
}

internal static class RequestIdentity
{
    public static Guid? AccountId(this ClaimsPrincipal principal) => ReadId(principal, JwtRegisteredClaimNames.Sub);
    public static Guid? SessionId(this ClaimsPrincipal principal) => ReadId(principal, "sid");
    public static RequestActor Actor(this HttpContext context) => new(context.User.AccountId(), context.TraceIdentifier);

    private static Guid? ReadId(ClaimsPrincipal principal, string claim) =>
        principal.Identity?.IsAuthenticated == true && Guid.TryParse(principal.FindFirstValue(claim), out var id) ? id : null;
}
