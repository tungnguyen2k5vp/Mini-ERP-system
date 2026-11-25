using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Api.Auth;
using MiniErp.Application.Auth;
using MiniErp.Infrastructure.Auth;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.IntegrationTests;

[CollectionDefinition("SqlServer", DisableParallelization = true)]
public sealed class SqlServerCollection : ICollectionFixture<ErpFixture>;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ConnectionStrings__Erp") is null)
        {
            Skip = "Set a MiniErp_Test SQL Server connection with scripts/test-all.ps1.";
        }
    }
}

public sealed class ErpFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static string Origin => Environment.GetEnvironmentVariable("Auth__PublicOrigin") ?? "http://localhost:5186";
    public string AdminEmail => Environment.GetEnvironmentVariable("Seed__AdminEmail")!;
    public string AdminPassword => Environment.GetEnvironmentVariable("Seed__AdminPassword")!;

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");

    async Task IAsyncLifetime.InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Erp");
        if (connection is null)
        {
            return;
        }

        var name = new SqlConnectionStringBuilder(connection).InitialCatalog;
        Assert.True(name == "MiniErp_Test" || name.StartsWith("MiniErp_Test_", StringComparison.Ordinal),
            "Integration tests require a separate MiniErp_Test database.");
        Assert.False(string.IsNullOrEmpty(AdminEmail) || string.IsNullOrEmpty(AdminPassword),
            "Integration tests require explicit seed credentials.");

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seed = scope.ServiceProvider.GetRequiredService<IdentityProvisioner>();
        await db.Database.MigrateAsync();
        await seed.EnsureRolesAsync(CancellationToken.None);
        await seed.EnsureAdministratorAsync(AdminEmail, AdminPassword, CancellationToken.None);
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    public HttpClient NewClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(Origin), HandleCookies = false });

    public async Task<HttpClient> AuthenticatedClientAsync(string? email = null, string? password = null)
    {
        var client = NewClient();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
            {
                Content = JsonContent.Create(new LoginRequest(email ?? AdminEmail, password ?? AdminPassword))
            };
            request.Headers.Add("Origin", Origin);
            request.Headers.Add("X-MiniErp-Session", "1");
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var session = (await response.Content.ReadFromJsonAsync<SessionResponse>())!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
