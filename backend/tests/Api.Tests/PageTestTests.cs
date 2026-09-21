using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
using QaTestOrchestrator.Infrastructure;
using Xunit;
namespace QaTestOrchestrator.Tests;
public sealed class PageTestTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static ProjectTestHost Host() => new(services => services.Replace(ServiceDescriptor.Singleton(new RunnerSettings { AllowedOrigins = ["http://127.0.0.1:5180"] })));
    private static object Input(RunDto seed, string url = "http://127.0.0.1:5180/catalog") => new { environmentId = seed.EnvironmentId, url, checks = new[] { "load", "console", "layout" }, devices = new[] { "desktop", "mobile" } };
    [Fact]
    public async Task Creates_pending_page_snapshot_and_reuses_catalog_without_changing_environment()
    {
        await using var host = Host(); using var client = host.CreateClient(); var seed = await RunnerTests.CreateRun(client);
        var path = $"/api/projects/{seed.ProjectId}/page-tests";
        var response = await client.PostAsJsonAsync(path, Input(seed)); response.EnsureSuccessStatusCode();
        var run = (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;
        Assert.Equal(RunStatus.Pending, run.Status); Assert.Equal("http://127.0.0.1:5180/catalog", run.Configuration.PageUrl);
        Assert.Equal(seed.Configuration.BaseUrl, run.Configuration.BaseUrl); Assert.Equal(6, run.Configuration.Cases.Length);
        Assert.Equal(seed.CreatedById, run.CreatedById); Assert.Equal(CapturePolicy.Always, run.Configuration.Options.Screenshot);
        var second = (await (await client.PostAsJsonAsync(path, Input(seed))).Content.ReadFromJsonAsync<RunDto>(Json))!;
        Assert.Equal(run.TestSuiteId, second.TestSuiteId);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
        Assert.Equal(1, await db.TestSuites.CountAsync(x => x.IsPageAudit)); Assert.Equal(9, await db.TestCases.CountAsync(x => x.TestSuiteId == run.TestSuiteId));
        Assert.Equal(seed.Configuration.BaseUrl, (await db.ProjectEnvironments.SingleAsync()).BaseUrl);
    }
    [Fact]
    public async Task Invalid_urls_selections_and_cross_project_environment_leave_no_catalog_or_run()
    {
        await using var host = Host(); using var client = host.CreateClient(); var seed = await RunnerTests.CreateRun(client);
        var path = $"/api/projects/{seed.ProjectId}/page-tests";
        foreach (var url in new[] { "file:///secret", "http://user:secret@127.0.0.1:5180/", "http://127.0.0.1:5180/?token=secret", "http://127.0.0.1:5180/#x", "http://127.0.0.1:9999/", "https://unapproved.example/" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, Input(seed, url))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { environmentId = seed.EnvironmentId, url = "http://127.0.0.1:5180/", checks = new[] { "custom-script" }, devices = new[] { "mobile" } })).StatusCode);
        var other = (await (await client.PostAsJsonAsync("/api/projects", new { name = "Other" })).Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/projects/{other.Id}/page-tests", Input(seed))).StatusCode);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
        Assert.Equal(1, await db.TestRuns.CountAsync()); Assert.False(await db.TestSuites.AnyAsync(x => x.IsPageAudit));
    }
    [Fact]
    public async Task Inactive_catalog_archived_project_disabled_environment_and_reader_are_rejected()
    {
        await using var host = Host(); using var client = host.CreateClient(); var seed = await RunnerTests.CreateRun(client);
        var path = $"/api/projects/{seed.ProjectId}/page-tests";
        (await client.PostAsJsonAsync(path, Input(seed))).EnsureSuccessStatusCode();
        using (var scope = host.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>(); var suite = await db.TestSuites.SingleAsync(x => x.IsPageAudit); suite.Edit(suite.Name, suite.Description, suite.Tags, TestSuiteStatus.Inactive, DateTime.UtcNow); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, Input(seed))).StatusCode);
        await AuthTestTools.Create(client, "reader"); using var reader = host.AnonymousClient(); await AuthTestTools.Login(reader, "reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync(path, Input(seed))).StatusCode);
        var env = (await client.GetFromJsonAsync<EnvironmentDto[]>($"/api/projects/{seed.ProjectId}/environments", Json))!.Single();
        (await client.PutAsJsonAsync($"/api/environments/{env.Id}", new { name = env.Name, baseUrl = env.BaseUrl, enabled = false, env.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, Input(seed))).StatusCode);
        var project = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{seed.ProjectId}"))!;
        (await client.PostAsJsonAsync($"/api/projects/{seed.ProjectId}/archive", new { project.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, Input(seed))).StatusCode);
    }
    [Fact]
    public async Task Runner_policy_rejects_page_origin_override_and_production()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "automation", "catalog.json"))) directory = directory.Parent;
        var policy = new RunnerPolicy(new RunnerSettings { Enabled = true, Root = Path.Combine(directory!.FullName, "automation"), AllowedOrigins = ["http://127.0.0.1:5180"] });
        await using var host = Host(); using var client = host.CreateClient(); var seed = await RunnerTests.CreateRun(client);
        var run = (await (await client.PostAsJsonAsync($"/api/projects/{seed.ProjectId}/page-tests", Input(seed))).Content.ReadFromJsonAsync<RunDto>(Json))!;
        policy.Validate(run.Configuration);
        Assert.Throws<ValidationException>(() => policy.Validate(run.Configuration with { PageUrl = "http://127.0.0.1:9999/" }));
        Assert.Throws<ValidationException>(() => policy.Validate(run.Configuration with { EnvironmentName = "Production" }));
    }
}
