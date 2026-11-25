using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MiniErp.Api.Auth;
using MiniErp.Application.Auth;

namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class IdentityTests(ErpFixture fixture)
{
    [SqlServerFact]
    public async Task Session_rotates_and_logout_revokes_the_bearer_token()
    {
        using var client = fixture.NewClient();
        using var login = await SendSessionAsync(client, "login", new LoginRequest(fixture.AdminEmail, fixture.AdminPassword));
        var session = await ReadAsync<SessionResponse>(login);
        var cookie = ReadCookie(login);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);

        using var invalidOrigin = await SendSessionAsync(client, "refresh", cookie: cookie, origin: "https://untrusted.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, invalidOrigin.StatusCode);

        using var rotated = await SendSessionAsync(client, "refresh", cookie: cookie);
        var next = await ReadAsync<SessionResponse>(rotated);
        Assert.Equal(session.User.Id, next.User.Id);

        using var replay = await SendSessionAsync(client, "refresh", cookie: cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", next.AccessToken);
        using var profile = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

        using var logout = await SendSessionAsync(client, "logout", cookie: ReadCookie(rotated));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var revoked = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [SqlServerFact]
    public async Task Admin_provisions_viewer_and_lock_revokes_existing_sessions()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var email = $"viewer-{Guid.NewGuid():N}@minierp.example";
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa5!";
        using var created = await admin.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest(email, "Người xem kiểm thử", password, "Viewer"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = await ReadAsync<UserAccount>(created);

        using var viewer = fixture.NewClient();
        using var login = await SendSessionAsync(viewer, "login", new LoginRequest(email, password));
        viewer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await ReadAsync<SessionResponse>(login)).AccessToken);
        using var denied = await viewer.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True(denied.Headers.Contains("X-Correlation-ID"));

        using var locked = await admin.PutAsJsonAsync($"/api/v1/users/{account.Id}",
            new UpdateUserRequest("Viewer", true, account.Version));
        Assert.True((await ReadAsync<UserAccount>(locked)).IsDisabled);
        using var conflict = await admin.PutAsJsonAsync($"/api/v1/users/{account.Id}",
            new UpdateUserRequest("Viewer", false, account.Version));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var inactive = await viewer.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);
        using var rejected = await SendSessionAsync(viewer, "login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    [SqlServerFact]
    public async Task Login_validates_input_and_never_returns_tokens_on_failure()
    {
        using var client = fixture.NewClient();
        using var invalid = await SendSessionAsync(client, "login", new { email = "invalid", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var wrong = await SendSessionAsync(client, "login", new LoginRequest(fixture.AdminEmail, "wrong"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.False(wrong.Headers.Contains("Set-Cookie"));
        Assert.StartsWith("application/problem+json", wrong.Content.Headers.ContentType?.ToString());
    }

    private static async Task<HttpResponseMessage> SendSessionAsync(HttpClient client, string route, object? body = null,
        string? cookie = null, string? origin = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/" + route);
        request.Headers.Add("Origin", origin ?? ErpFixture.Origin);
        request.Headers.Add("X-MiniErp-Session", "1");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie.Split(';')[0]);
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return await client.SendAsync(request);
    }

    private static string ReadCookie(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
