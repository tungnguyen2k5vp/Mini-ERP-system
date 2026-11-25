using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MiniErp.Api;
using MiniErp.Api.Auth;
using MiniErp.Api.Catalog;
using MiniErp.Api.Inventory;
using MiniErp.Application.Auth;
using MiniErp.Infrastructure.Auth;
using MiniErp.Infrastructure.Catalog;
using MiniErp.Infrastructure.Inventory;
using MiniErp.Infrastructure.Persistence;

var commands = new[] { "--migrate", "--seed", "--provision-admin" };
var builder = WebApplication.CreateBuilder(args.Where(arg => !commands.Contains(arg)).ToArray());
var connection = builder.Configuration.GetConnectionString("Erp")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Erp to the MiniErp SQL Server database.");

builder.Services.AddDbContext<ErpDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddIdentityCore<ErpUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<ErpDbContext>().AddSignInManager();

builder.Services.AddScoped<ProfileReader>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IIdentityAdministration, IdentityAdministration>();
builder.Services.AddScoped<IdentityProvisioner>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<CatalogSeed>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<InventoryReport>();
builder.Services.AddScoped<StockDemoSeed>();
builder.Services.AddSingleton<AccessTokenIssuer>();
builder.Services.AddOptions<AuthSettings>().BindConfiguration("Auth")
    .Validate(AuthSettings.HasValidKey, "Auth__SigningKey must be a Base64 key of at least 32 bytes.")
    .Validate(settings => settings.HasValidOrigin(builder.Environment.IsDevelopment()), "Auth__PublicOrigin must be a canonical HTTPS origin, or a development loopback HTTP origin.")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthSettings>>((jwt, auth) =>
    {
        jwt.MapInboundClaims = false;
        jwt.IncludeErrorDetails = false;
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = AuthSettings.Issuer,
            ValidAudience = AuthSettings.Audience,
            IssuerSigningKey = auth.Value.Key(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(15)
        };
        jwt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal!;
                var identity = context.HttpContext.RequestServices.GetRequiredService<IIdentityService>();
                if (principal.AccountId() is not Guid userId || principal.SessionId() is not Guid sessionId ||
                    !await identity.IsSessionActiveAsync(userId, sessionId, context.HttpContext.RequestAborted))
                {
                    context.Fail("Session ended.");
                }
            }
        };
    });

builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission)));
    }
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", http => ByAddress(http, 20));
    options.AddPolicy("session", http => ByAddress(http, 60));
});
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiErrors>();
builder.Services.AddValidation();
builder.Services.AddOpenApi("v1");
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("sql-server", tags: ["ready"]);

var app = builder.Build();
if (args.Any(commands.Contains))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
    var provisioner = scope.ServiceProvider.GetRequiredService<IdentityProvisioner>();
    if (args.Contains("--migrate"))
    {
        await db.Database.MigrateAsync();
        await provisioner.EnsureRolesAsync(CancellationToken.None);
    }

    if (args.Contains("--seed") || args.Contains("--provision-admin"))
    {
        if (args.Contains("--seed") && !app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException("Demo provisioning is restricted to Development or Testing.");
        }

        await provisioner.EnsureRolesAsync(CancellationToken.None);
        await provisioner.EnsureAdministratorAsync(
            app.Configuration["Seed:AdminEmail"] ?? throw new InvalidOperationException("Set Seed__AdminEmail."),
            app.Configuration["Seed:AdminPassword"] ?? throw new InvalidOperationException("Set Seed__AdminPassword."),
            CancellationToken.None);
        if (args.Contains("--seed"))
        {
            await scope.ServiceProvider.GetRequiredService<CatalogSeed>().SeedAsync(CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<StockDemoSeed>().SeedAsync(CancellationToken.None);
        }
    }
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (http, next) =>
{
    http.Response.Headers["X-Correlation-ID"] = http.TraceIdentifier;
    http.Response.Headers.XContentTypeOptions = "nosniff";
    http.Response.Headers["Referrer-Policy"] = "same-origin";
    await next(http);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
var api = app.MapGroup("/api/v1").RequireAuthorization();
api.MapIdentityEndpoints();
api.MapCatalog();
api.MapInventory();

await app.RunAsync();

static RateLimitPartition<string> ByAddress(HttpContext http, int limit) =>
    RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        });

internal sealed class DatabaseHealthCheck(ErpDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken) &&
        !(await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any()
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("SQL Server is unavailable or migrations are pending.");
}

public partial class Program;
