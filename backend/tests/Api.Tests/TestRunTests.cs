using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
using Xunit;

namespace QaTestOrchestrator.Tests;

public sealed class TestRunTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static RunOptions Options => new(TestType.Smoke, BrowserType.Chromium, ExecutionMode.Headless, 1, 0, 60, CapturePolicy.OnFailure, CapturePolicy.OnFailure, CapturePolicy.OnFailure);
    [Theory]
    [InlineData(0, 0, 60)] [InlineData(11, 0, 60)] [InlineData(1, -1, 60)] [InlineData(1, 6, 60)] [InlineData(1, 0, 4)] [InlineData(1, 0, 301)]
    public void Rejects_out_of_bounds_options(int workers, int retries, int timeout) => Assert.Throws<ValidationException>(() => (Options with { Workers = workers, Retries = retries, TimeoutSeconds = timeout }).Validate());

    [Fact]
    public void State_machine_preserves_terminal_states()
    {
        var run = TestRun.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{}", DateTime.UtcNow);
        Assert.Throws<RunStateException>(() => run.Start(DateTime.UtcNow));
        run.Queue(); run.Start(DateTime.UtcNow); run.Complete(RunStatus.Failed, DateTime.UtcNow);
        Assert.NotNull(run.StartedAt); Assert.NotNull(run.FinishedAt);
        Assert.Throws<RunStateException>(() => run.Cancel(DateTime.UtcNow));
        Assert.Throws<RunStateException>(() => run.Complete(RunStatus.Passed, DateTime.UtcNow));
        var cancelled = TestRun.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{}", DateTime.UtcNow);
        cancelled.Queue(); cancelled.Start(DateTime.UtcNow); cancelled.Cancel(DateTime.UtcNow);
        var version = cancelled.Version; cancelled.Cancel(DateTime.UtcNow); Assert.Equal(version, cancelled.Version);
        Assert.Throws<RunStateException>(() => cancelled.Complete(RunStatus.Passed, DateTime.UtcNow));
    }

    [Fact]
    public async Task Creates_pending_snapshot_filters_history_and_cancels_idempotently()
    {
        await using var host = new ProjectTestHost(); using var client = host.CreateClient();
        var (project, request) = await Setup(client);
        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/test-runs", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var run = (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;
        Assert.Equal(RunStatus.Pending, run.Status); Assert.False(run.RunnerAvailable); Assert.Null(run.StartedAt);
        Assert.Equal("https://staging.example.com/", run.Configuration.BaseUrl);
        Assert.Equal("login", run.Configuration.Cases.Single().StableKey);
        var test = (await client.GetFromJsonAsync<CaseDto>($"/api/test-cases/{request.CaseIds![0]}", Json))!;
        (await client.PutAsJsonAsync($"/api/test-cases/{test.Id}", new { name = "Alterado", status = "Inactive", test.Version })).EnsureSuccessStatusCode();
        var read = (await client.GetFromJsonAsync<RunDto>($"/api/test-runs/{run.Id}", Json))!;
        Assert.Equal("Login", read.Configuration.Cases.Single().Name);
        Assert.Equal(1, read.Configuration.Cases.Single().CatalogVersion);
        Assert.Single((await client.GetFromJsonAsync<RunPage>($"/api/test-runs?projectId={project.Id}&status=Pending&pageSize=1", Json))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<RunPage>("/api/test-runs?page=2&pageSize=1", Json))!.Items);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/test-runs/{run.Id}/cancel", new { version = Guid.NewGuid() })).StatusCode);
        var cancelled = (await (await client.PostAsJsonAsync($"/api/test-runs/{run.Id}/cancel", new { run.Version })).Content.ReadFromJsonAsync<RunDto>(Json))!;
        Assert.Equal(RunStatus.Cancelled, cancelled.Status); Assert.NotNull(cancelled.FinishedAt);
        var again = (await (await client.PostAsJsonAsync($"/api/test-runs/{run.Id}/cancel", new { run.Version })).Content.ReadFromJsonAsync<RunDto>(Json))!;
        Assert.Equal(cancelled.Version, again.Version);
        Assert.Single((await client.GetFromJsonAsync<RunPage>("/api/test-runs?status=Cancelled", Json))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/test-runs?status=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/test-runs/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Rejects_foreign_inactive_empty_duplicate_and_invalid_configuration()
    {
        await using var host = new ProjectTestHost(); using var client = host.CreateClient();
        var (project, request) = await Setup(client); var (_, other) = await Setup(client);
        var path = $"/api/projects/{project.Id}/test-runs";
        foreach (var invalid in new[] {
            request with { TestSuiteId = other.TestSuiteId }, request with { EnvironmentId = other.EnvironmentId },
            request with { CaseIds = other.CaseIds }, request with { CaseIds = [] }, request with { CaseIds = [request.CaseIds![0], request.CaseIds[0]] },
            request with { Options = null }, request with { Options = Options with { Workers = 11 } }, request with { Tags = ["@missing"] }, request with { Tags = ["bad"] } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, invalid, Json)).StatusCode);
        var suite = (await client.GetFromJsonAsync<SuiteDto>($"/api/test-suites/{request.TestSuiteId}", Json))!;
        (await client.PutAsJsonAsync($"/api/test-suites/{suite.Id}", new { suite.Name, status = "Inactive", suite.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, request, Json)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<RunPage>("/api/test-runs", Json))!.Items);
    }

    [Fact]
    public async Task Disabled_environment_and_archived_project_cannot_create_runs()
    {
        await using var host = new ProjectTestHost(); using var client = host.CreateClient();
        var (project, request) = await Setup(client);
        var env = (await client.GetFromJsonAsync<EnvironmentDto[]>($"/api/projects/{project.Id}/environments", Json))!.Single();
        (await client.PutAsJsonAsync($"/api/environments/{env.Id}", new { env.BaseUrl, enabled = false, env.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/projects/{project.Id}/test-runs", request, Json)).StatusCode);
        var (activeProject, activeRequest) = await Setup(client);
        var current = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{activeProject.Id}"))!;
        (await client.PostAsJsonAsync($"/api/projects/{current.Id}/archive", new { current.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/projects/{current.Id}/test-runs", activeRequest, Json)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_catalog_change_rolls_back_new_run()
    {
        await using var host = new ProjectTestHost(); using var client = host.CreateClient();
        var (project, request) = await Setup(client);
        await using var scope = host.Services.CreateAsyncScope();
        var loaded = (await scope.ServiceProvider.GetRequiredService<IProjectStore>().FindAsync(project.Id, default))!;
        (await client.PostAsJsonAsync($"/api/projects/{project.Id}/archive", new { loaded.Version })).EnsureSuccessStatusCode();
        await Assert.ThrowsAsync<RunConflictException>(() => scope.ServiceProvider.GetRequiredService<TestRunService>().CreateAsync(project.Id, request, default));
        Assert.Empty((await client.GetFromJsonAsync<RunPage>("/api/test-runs", Json))!.Items);
    }

    [Fact]
    public async Task Concurrent_completion_prevents_stale_cancellation()
    {
        await using var host = new ProjectTestHost(); using var client = host.CreateClient();
        var (project, request) = await Setup(client);
        var run = (await (await client.PostAsJsonAsync($"/api/projects/{project.Id}/test-runs", request, Json)).Content.ReadFromJsonAsync<RunDto>(Json))!;
        await using var staleScope = host.Services.CreateAsyncScope();
        await staleScope.ServiceProvider.GetRequiredService<ITestRunStore>().FindAsync(run.Id, default);
        await using var workerScope = host.Services.CreateAsyncScope();
        var store = workerScope.ServiceProvider.GetRequiredService<ITestRunStore>(); var entity = (await store.FindAsync(run.Id, default))!;
        entity.Queue(); entity.Start(DateTime.UtcNow); entity.Complete(RunStatus.Passed, DateTime.UtcNow); await store.SaveAsync(default);
        await Assert.ThrowsAsync<RunConflictException>(() => staleScope.ServiceProvider.GetRequiredService<TestRunService>().CancelAsync(run.Id, new(run.Version), default));
        Assert.Equal(RunStatus.Passed, (await client.GetFromJsonAsync<RunDto>($"/api/test-runs/{run.Id}", Json))!.Status);
    }

    private static async Task<(ProjectDto, RunRequest)> Setup(HttpClient client)
    {
        var project = (await (await client.PostAsJsonAsync("/api/projects", new { name = "Portal" })).Content.ReadFromJsonAsync<ProjectDto>())!;
        var suite = (await (await client.PostAsJsonAsync($"/api/projects/{project.Id}/test-suites", new { name = "Smoke" })).Content.ReadFromJsonAsync<SuiteDto>(Json))!;
        var test = (await (await client.PostAsJsonAsync($"/api/test-suites/{suite.Id}/test-cases", new { stableKey = "login", name = "Login", tags = new[] { "@smoke" } })).Content.ReadFromJsonAsync<CaseDto>(Json))!;
        var env = (await (await client.PostAsJsonAsync($"/api/projects/{project.Id}/environments", new { name = "Staging", baseUrl = "https://staging.example.com", enabled = true })).Content.ReadFromJsonAsync<EnvironmentDto>(Json))!;
        return (project, new(suite.Id, env.Id, [test.Id], ["@smoke"], Options));
    }
}
