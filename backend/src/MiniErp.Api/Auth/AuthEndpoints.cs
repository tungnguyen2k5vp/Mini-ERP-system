using Microsoft.Extensions.Options;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Domain;

namespace MiniErp.Api.Auth;

internal static class AuthEndpoints
{
    private const string RefreshCookie = "mini_erp_refresh";
    private const string AuthPath = "/api/v1/auth";

    public static void MapIdentityEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Authentication").AddEndpointFilter(RequireFirstParty);
        auth.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting("login")
            .Produces<SessionResponse>().ProducesProblem(401).ProducesProblem(429).WithName("Login");
        auth.MapPost("/refresh", RefreshAsync).AllowAnonymous().RequireRateLimiting("session")
            .Produces<SessionResponse>().ProducesProblem(401).WithName("Refresh");
        auth.MapPost("/logout", LogoutAsync).AllowAnonymous().RequireRateLimiting("session")
            .Produces(204).WithName("Logout");
        auth.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization()
            .Produces(204).ProducesProblem(400).WithName("ChangePassword");

        api.MapGet("/auth/me", async (HttpContext http, IIdentityService identity, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return await identity.GetProfileAsync(http.User.AccountId()!.Value, ct) is { } profile
                ? Results.Ok(profile)
                : Results.Unauthorized();
        }).WithTags("Authentication").Produces<UserProfile>().WithName("GetProfile");

        var users = api.MapGroup("/users").WithTags("Users").RequireAuthorization(Permissions.ManageUsers);
        users.MapGet("/", async (IIdentityAdministration admin, string? search, int? page, int? pageSize, CancellationToken ct) =>
        {
            var (number, size) = ApiPaging.Validate(page, pageSize, search);
            return Results.Ok(await admin.ListUsersAsync(search, number, size, ct));
        }).Produces<Page<UserAccount>>().WithName("ListUsers");
        users.MapPost("/", async (CreateUserRequest request, HttpContext http, IIdentityAdministration admin, CancellationToken ct) =>
        {
            var user = await admin.CreateUserAsync(request, http.Actor(), ct);
            return Results.Created($"/api/v1/users/{user.Id}", user);
        }).Produces<UserAccount>(201).WithName("CreateUser");
        users.MapGet("/{id:guid}", async (Guid id, IIdentityService identity, CancellationToken ct) =>
            await identity.GetProfileAsync(id, ct) is { } user ? Results.Ok(user) : Results.NotFound())
            .Produces<UserProfile>().WithName("GetUser");
        users.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, HttpContext http,
            IIdentityAdministration admin, CancellationToken ct) =>
            Results.Ok(await admin.UpdateUserAsync(id, request, http.Actor(), ct)))
            .Produces<UserAccount>().ProducesProblem(409).WithName("UpdateUserAccess");

        var roles = api.MapGroup("/roles").WithTags("Roles").RequireAuthorization(Permissions.ManageRoles);
        roles.MapGet("/", async (IIdentityAdministration admin, CancellationToken ct) =>
            Results.Ok(await admin.ListRolesAsync(ct))).Produces<RoleAccess[]>().WithName("ListRoles");
        roles.MapPut("/{name}", async (string name, UpdateRoleRequest request, HttpContext http,
            IIdentityAdministration admin, CancellationToken ct) =>
            Results.Ok(await admin.UpdateRoleAsync(name, request, http.Actor(), ct)))
            .Produces<RoleAccess>().ProducesProblem(409).WithName("UpdateRolePermissions");
        api.MapGet("/permissions", () => Results.Ok(Permissions.All)).WithTags("Roles")
            .RequireAuthorization(Permissions.ManageRoles).WithName("ListPermissions");
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, HttpContext http, IIdentityService identity,
        AccessTokenIssuer tokens, IHostEnvironment environment, CancellationToken ct)
    {
        var session = await identity.LoginAsync(request, new RequestActor(null, http.TraceIdentifier), ct);
        return session is null
            ? Results.Problem(statusCode: 401, title: "Đăng nhập thất bại.",
                detail: "Kiểm tra thông tin đăng nhập hoặc liên hệ quản trị viên.")
            : SetSession(http, session, tokens, environment);
    }

    private static async Task<IResult> RefreshAsync(HttpContext http, IIdentityService identity,
        AccessTokenIssuer tokens, IHostEnvironment environment, CancellationToken ct)
    {
        var rawToken = http.Request.Cookies[RefreshCookie];
        var session = rawToken is null ? null : await identity.RefreshAsync(rawToken, ct);
        return session is null ? Results.Unauthorized() : SetSession(http, session, tokens, environment);
    }

    private static async Task<IResult> LogoutAsync(HttpContext http, IIdentityService identity,
        IHostEnvironment environment, CancellationToken ct)
    {
        await identity.LogoutAsync(http.Request.Cookies[RefreshCookie], http.User.SessionId(), http.Actor(), ct);
        http.Response.Cookies.Delete(RefreshCookie, Cookie(environment, http));
        return Results.NoContent();
    }

    private static async Task<IResult> ChangePasswordAsync(ChangePasswordRequest request, HttpContext http,
        IIdentityService identity, IHostEnvironment environment, CancellationToken ct)
    {
        await identity.ChangePasswordAsync(http.User.AccountId()!.Value, request, http.Actor(), ct);
        http.Response.Cookies.Delete(RefreshCookie, Cookie(environment, http));
        return Results.NoContent();
    }

    private static IResult SetSession(HttpContext http, IssuedSession session, AccessTokenIssuer tokens,
        IHostEnvironment environment)
    {
        var options = Cookie(environment, http);
        options.Expires = session.RefreshExpiresAt;
        http.Response.Cookies.Append(RefreshCookie, session.RefreshToken, options);
        return Results.Ok(tokens.Issue(session));
    }

    private static CookieOptions Cookie(IHostEnvironment environment, HttpContext http) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment() || http.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = AuthPath,
        IsEssential = true
    };

    private static ValueTask<object?> RequireFirstParty(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        var auth = http.RequestServices.GetRequiredService<IOptions<AuthSettings>>().Value;
        if (http.Request.Headers.Origin.Count != 1 || http.Request.Headers.Origin[0] != auth.PublicOrigin ||
            http.Request.Headers["X-MiniErp-Session"].ToString() != "1")
        {
            return ValueTask.FromResult<object?>(Results.Problem(statusCode: 403, title: "Yêu cầu phiên không hợp lệ."));
        }
        return next(context);
    }
}

internal static class ApiPaging
{
    public static (int Page, int Size) Validate(int? page, int? size, string? search = null)
    {
        if (page is < 1 or > 100_000 || size is < 1 or > 100 || search?.Length > 120)
        {
            throw ErpException.Invalid("invalid_paging", "Tham số tìm kiếm hoặc phân trang không hợp lệ.");
        }

        return (page ?? 1, size ?? 25);
    }
}
