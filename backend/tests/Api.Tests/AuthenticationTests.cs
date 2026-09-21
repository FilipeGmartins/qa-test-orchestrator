using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QaTestOrchestrator.Api;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Infrastructure;
using Xunit;
namespace QaTestOrchestrator.Tests;

internal static class AuthTestTools
{
    public const string Password = "Testing-only-password-42";
    public static async Task Csrf(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", token.GetProperty("token").GetString());
    }
    public static async Task Login(HttpClient client, string login, string password = Password)
    {
        await Csrf(client);
        (await client.PostAsJsonAsync("/api/auth/login", new { login, password })).EnsureSuccessStatusCode();
        await Csrf(client);
    }
    public static async Task<AccountDto> Create(HttpClient admin, string login, string role = "Reader") =>
        (await (await admin.PostAsJsonAsync("/api/users", new { login, name = login, role, password = Password })).Content.ReadFromJsonAsync<AccountDto>())!;
}
public sealed class AuthenticationTests
{
    [Fact]
    public async Task Anonymous_requests_cannot_read_or_mutate_workspace_or_evidence()
    {
        await using var host = new ProjectTestHost(); using var client = host.AnonymousClient();
        foreach (var path in new[] { "/api/projects", "/api/test-runs", "/api/dashboard", "/api/runner", "/api/system", "/api/users", "/openapi/v1.json", $"/api/test-runs/{Guid.NewGuid()}/artifacts/{Guid.NewGuid()}" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/projects", new { name = "forbidden" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/live")).StatusCode);
    }
    [Fact]
    public async Task Login_requires_csrf_cookie_and_token_and_logout_revokes_session()
    {
        await using var host = new ProjectTestHost(); using var client = host.AnonymousClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { login = "test.admin", password = AuthTestTools.Password })).StatusCode);
        await AuthTestTools.Login(client, "test.admin");
        Assert.Equal("Admin", (await client.GetFromJsonAsync<AccountDto>("/api/auth/me"))!.Role);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/projects", new { name = "csrf" })).StatusCode);
        await AuthTestTools.Csrf(client);
        (await client.PostAsJsonAsync("/api/auth/logout", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/projects")).StatusCode);
        using var scope = host.Services.CreateScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>().LoginSessions.ToListAsync());
    }
    [Fact]
    public async Task Reader_cannot_write_operator_cannot_manage_users_and_authorship_is_server_owned()
    {
        await using var host = new ProjectTestHost(); using var admin = host.CreateClient();
        await AuthTestTools.Create(admin, "reader"); var user = await AuthTestTools.Create(admin, "operator", "Operator");
        using var reader = host.AnonymousClient(); await AuthTestTools.Login(reader, "reader");
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/projects", new { name = "denied" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"/api/test-runs/{Guid.NewGuid()}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/users")).StatusCode);
        using var op = host.AnonymousClient(); await AuthTestTools.Login(op, "operator");
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync("/api/users", new { })).StatusCode);
        var run = await RunnerTests.CreateRun(op);
        Assert.Equal(user.Id, run.CreatedById); Assert.Equal("operator", run.CreatedByName);
        var cancelled = await (await op.PostAsJsonAsync($"/api/test-runs/{run.Id}/cancel", new { run.Version, cancelledByName = "spoof" })).Content.ReadFromJsonAsync<RunDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
        Assert.Equal(user.Id, cancelled!.CancelledById);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/test-runs/{run.Id}")).StatusCode);
    }
    [Fact]
    public async Task User_changes_revoke_existing_sessions_and_last_admin_is_preserved()
    {
        await using var host = new ProjectTestHost(); using var admin = host.CreateClient();
        var reader = await AuthTestTools.Create(admin, "reader"); using var client = host.AnonymousClient(); await AuthTestTools.Login(client, "reader");
        (await admin.PutAsJsonAsync($"/api/users/{reader.Id}", new { reader.Login, reader.Name, role = "Operator", active = false, reader.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/users/{reader.Id}", new { reader.Login, reader.Name, reader.Role, active = true, reader.Version })).StatusCode);
        var me = (await admin.GetFromJsonAsync<AccountDto>("/api/auth/me"))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/users/{me.Id}", new { me.Login, me.Name, role = "Reader", active = true, me.Version })).StatusCode);
    }
    [Fact]
    public async Task Password_change_requires_current_password_and_revokes_all_sessions()
    {
        await using var host = new ProjectTestHost(); using var a = host.CreateClient(); using var b = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.PostAsJsonAsync("/api/auth/password", new { currentPassword = "bad", newPassword = "replacement-password-42" })).StatusCode);
        (await a.PostAsJsonAsync("/api/auth/password", new { currentPassword = AuthTestTools.Password, newPassword = "replacement-password-42" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.GetAsync("/api/projects")).StatusCode);
        await AuthTestTools.Login(a, "test.admin", "replacement-password-42");
    }
    [Fact]
    public async Task Invalid_login_locks_account_and_sessions_expire()
    {
        await using var host = new ProjectTestHost(); using var client = host.AnonymousClient(); await AuthTestTools.Csrf(client);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { login = "test.admin", password = "incorrect" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { login = "test.admin", password = AuthTestTools.Password })).StatusCode);
        using (var scope = host.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>(); var user = await db.Accounts.SingleAsync();
            Assert.NotNull(user.LockedUntil); Assert.DoesNotContain(AuthTestTools.Password, user.PasswordHash); user.LockedUntil = DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
        }
        await AuthTestTools.Login(client, "test.admin");
        using (var scope = host.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>(); (await db.LoginSessions.SingleAsync()).ExpiresAt = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/projects")).StatusCode);
    }
    [Fact]
    public async Task Logout_rejects_replayed_cookie_and_csrf_token_from_another_browser()
    {
        await using var host = new ProjectTestHost(); using var client = host.AnonymousClient();
        await AuthTestTools.Csrf(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { login = "test.admin", password = AuthTestTools.Password }); response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("qa.session=")).Split(';')[0];
        Assert.Contains("httponly", response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("qa.session=")).ToLowerInvariant());
        using var other = host.AnonymousClient(); await AuthTestTools.Csrf(other);
        client.DefaultRequestHeaders.Remove("X-CSRF-Token"); client.DefaultRequestHeaders.Add("X-CSRF-Token", other.DefaultRequestHeaders.GetValues("X-CSRF-Token"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/projects", new { name = "forged" })).StatusCode);
        await AuthTestTools.Csrf(client); (await client.PostAsJsonAsync("/api/auth/logout", new { })).EnsureSuccessStatusCode();
        other.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/auth/me")).StatusCode);
    }
    [Fact]
    public async Task Administrative_registry_fences_concurrent_changes_and_login_is_rate_limited()
    {
        await using var host = new ProjectTestHost(); using var admin = host.CreateClient();
        await using var a = host.Services.CreateAsyncScope(); await using var b = host.Services.CreateAsyncScope();
        var dbA = a.ServiceProvider.GetRequiredService<OrchestratorDbContext>(); var dbB = b.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
        var first = await dbA.AccountRegistries.SingleAsync(); var second = await dbB.AccountRegistries.SingleAsync();
        first.Version = Guid.NewGuid(); second.Version = Guid.NewGuid(); await dbA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
        using var client = host.AnonymousClient(); await AuthTestTools.Csrf(client);
        HttpStatusCode status = HttpStatusCode.OK;
        for (var i = 0; i < 31; i++) status = (await client.PostAsJsonAsync("/api/auth/login", new { login = "absent", password = "incorrect" })).StatusCode;
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
    }
    [Fact]
    public async Task User_validation_and_bootstrap_do_not_expose_passwords_or_allow_reinitialization()
    {
        await using var host = new ProjectTestHost(); using var admin = host.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/users", new { login = "valid", name = "User", role = "Admin", password = "short" })).StatusCode);
        await AuthTestTools.Create(admin, "reader");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/users", new { login = "READER", name = "User", role = "Admin", password = AuthTestTools.Password })).StatusCode);
        var body = await admin.GetStringAsync("/api/users"); Assert.DoesNotContain("password", body.ToLowerInvariant());
        using var scope = host.Services.CreateScope();
        await Assert.ThrowsAsync<QaTestOrchestrator.Domain.ValidationException>(() => scope.ServiceProvider.GetRequiredService<AccountService>().Bootstrap("other", "Other", AuthTestTools.Password, default));
    }
}
