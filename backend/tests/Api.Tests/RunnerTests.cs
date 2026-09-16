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

public sealed class RunnerTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private sealed class AllowedPolicy : IRunnerPolicy
    {
        public bool Enabled => true;
        public IReadOnlyList<ExecutableCase> Catalog => [new("page-title", "Title", ["Smoke"], 1)];
        public void Validate(RunSnapshot snapshot) { }
    }
    private sealed class ControlledRunner : ITestRunner
    {
        public int Calls;
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WaitForCancellation;
        public async Task<string> ExecuteAsync(Guid id, RunSnapshot snapshot, Func<string, Task> progress, CancellationToken ct)
        {
            Calls++;
            await progress("{\"kind\":\"begin\",\"total\":1}");
            Started.TrySetResult();
            if (WaitForCancellation) await Task.Delay(Timeout.Infinite, ct);
            return "{\"kind\":\"end\",\"status\":\"passed\",\"total\":1,\"passed\":1,\"failed\":0,\"skipped\":0}";
        }
    }
    [Fact]
    public async Task Enqueue_is_explicit_idempotent_worker_processes_once_and_records_result()
    {
        var runner = new ControlledRunner();
        await using var host = new ProjectTestHost(services => { services.Replace(ServiceDescriptor.Singleton<IRunnerPolicy>(new AllowedPolicy())); services.Replace(ServiceDescriptor.Singleton<ITestRunner>(runner)); });
        using var client = host.CreateClient();
        var run = await CreateRun(client);
        var worker = host.Services.GetRequiredService<RunWorker>();
        Assert.False(await worker.ProcessNextAsync(default)); // Pending never auto-runs.
        var queued = await client.PostAsJsonAsync($"/api/test-runs/{run.Id}/enqueue", new { run.Version }); queued.EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/test-runs/{run.Id}/enqueue", new { run.Version })).EnsureSuccessStatusCode();
        Assert.True(await worker.ProcessNextAsync(default)); Assert.False(await worker.ProcessNextAsync(default)); Assert.Equal(1, runner.Calls);
        var saved = (await client.GetFromJsonAsync<RunDto>($"/api/test-runs/{run.Id}", Json))!;
        Assert.Equal(RunStatus.Passed, saved.Status); Assert.Equal(1, saved.Result!.Value.GetProperty("passed").GetInt32());
        Assert.NotEmpty(saved.Progress!.Value.EnumerateArray());
    }
    [Fact]
    public async Task Running_cancellation_stops_adapter_before_marking_cancelled()
    {
        var runner = new ControlledRunner { WaitForCancellation = true };
        await using var host = new ProjectTestHost(services => { services.Replace(ServiceDescriptor.Singleton<IRunnerPolicy>(new AllowedPolicy())); services.Replace(ServiceDescriptor.Singleton<ITestRunner>(runner)); });
        using var client = host.CreateClient(); var run = await CreateRun(client);
        (await client.PostAsJsonAsync($"/api/test-runs/{run.Id}/enqueue", new { run.Version })).EnsureSuccessStatusCode();
        var work = host.Services.GetRequiredService<RunWorker>().ProcessNextAsync(default);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var running = (await client.GetFromJsonAsync<RunDto>($"/api/test-runs/{run.Id}", Json))!;
        var cancel = await client.PostAsJsonAsync($"/api/test-runs/{run.Id}/cancel", new { running.Version }); cancel.EnsureSuccessStatusCode();
        var requested = (await cancel.Content.ReadFromJsonAsync<RunDto>(Json))!;
        Assert.True(requested.CancellationRequested); Assert.Equal(RunStatus.Running, requested.Status);
        await work.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(RunStatus.Cancelled, (await client.GetFromJsonAsync<RunDto>($"/api/test-runs/{run.Id}", Json))!.Status);
    }
    [Fact]
    public async Task Expired_lease_is_error_without_replaying_tests()
    {
        var runner = new ControlledRunner();
        await using var host = new ProjectTestHost(services => services.Replace(ServiceDescriptor.Singleton<ITestRunner>(runner)));
        using var client = host.CreateClient(); var run = await CreateRun(client);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>(); var entity = await db.TestRuns.SingleAsync();
            entity.Queue(); entity.Claim(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-2)); await db.SaveChangesAsync();
        }
        Assert.False(await host.Services.GetRequiredService<RunWorker>().ProcessNextAsync(default));
        var saved = (await client.GetFromJsonAsync<RunDto>($"/api/test-runs/{run.Id}", Json))!;
        Assert.Equal(RunStatus.Error, saved.Status); Assert.Equal(0, runner.Calls);
    }
    [Fact]
    public async Task Enqueue_rejects_catalog_changes_and_disabled_runner()
    {
        await using var host = new ProjectTestHost(); using var client = host.CreateClient(); var run = await CreateRun(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/test-runs/{run.Id}/enqueue", new { run.Version })).StatusCode);
        var policy = new RunnerPolicy(new RunnerSettings { Enabled = true, Root = Path.GetTempPath(), AllowedOrigins = ["http://127.0.0.1:5180"] });
        Assert.Throws<ValidationException>(() => policy.Validate(run.Configuration));
        await using var enabledHost = new ProjectTestHost(services => services.Replace(ServiceDescriptor.Singleton<IRunnerPolicy>(new AllowedPolicy())));
        using var enabledClient = enabledHost.CreateClient(); var second = await CreateRun(enabledClient);
        var item = second.Configuration.Cases.Single();
        var original = (await enabledClient.GetFromJsonAsync<CaseDto>($"/api/test-cases/{item.Id}", Json))!;
        (await enabledClient.PutAsJsonAsync($"/api/test-cases/{item.Id}", new { name = "Changed", status = "Active", original.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await enabledClient.PostAsJsonAsync($"/api/test-runs/{second.Id}/enqueue", new { second.Version })).StatusCode);
    }
    [Fact]
    public async Task Only_one_competing_worker_can_claim_a_queued_run()
    {
        await using var host = new ProjectTestHost(services => services.Replace(ServiceDescriptor.Singleton<IRunnerPolicy>(new AllowedPolicy())));
        using var client = host.CreateClient(); var run = await CreateRun(client);
        (await client.PostAsJsonAsync($"/api/test-runs/{run.Id}/enqueue", new { run.Version })).EnsureSuccessStatusCode();
        await using var scopeA = host.Services.CreateAsyncScope(); await using var scopeB = host.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<OrchestratorDbContext>(); var dbB = scopeB.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
        var a = await dbA.TestRuns.SingleAsync(); var b = await dbB.TestRuns.SingleAsync();
        var winner = Guid.NewGuid(); a.Claim(winner, DateTime.UtcNow); b.Claim(Guid.NewGuid(), DateTime.UtcNow);
        await dbA.SaveChangesAsync(); await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
        await using var checkScope = host.Services.CreateAsyncScope();
        Assert.Equal(winner, (await checkScope.ServiceProvider.GetRequiredService<OrchestratorDbContext>().TestRuns.SingleAsync()).LeaseId);
    }

    [RealRunnerFact]
    public async Task Real_adapter_connects_api_queue_chromium_and_persistence()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "automation", "catalog.json"))) directory = directory.Parent;
        Assert.NotNull(directory);
        using var server = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        server.Start();
        var origin = $"http://127.0.0.1:{((System.Net.IPEndPoint)server.LocalEndpoint).Port}";
        using var serverStop = new CancellationTokenSource();
        var serving = Task.Run(async () => {
            while (!serverStop.IsCancellationRequested)
            {
                using var socket = await server.AcceptTcpClientAsync(serverStop.Token);
                var stream = socket.GetStream();
                using var reader = new StreamReader(stream, leaveOpen: true);
                while (await reader.ReadLineAsync(serverStop.Token) is { Length: > 0 }) { }
                var body = "<html><title>Real integration</title></html>";
                var reply = System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}");
                await stream.WriteAsync(reply, serverStop.Token);
            }
        });
        try
        {
            var settings = new RunnerSettings { Enabled = true, Root = Path.Combine(directory.FullName, "automation"), ArtifactsRoot = Path.Combine(directory.FullName, ".cache", "integration-artifacts"), AllowedOrigins = [origin] };
            await using var host = new ProjectTestHost(services => services.Replace(ServiceDescriptor.Singleton(settings)));
            using var client = host.CreateClient(); var run = await CreateRun(client, origin, capture: true);
            (await client.PostAsJsonAsync($"/api/test-runs/{run.Id}/enqueue", new { run.Version })).EnsureSuccessStatusCode();
            Assert.True(await host.Services.GetRequiredService<RunWorker>().ProcessNextAsync(default));
            var result = (await client.GetFromJsonAsync<RunDto>($"/api/test-runs/{run.Id}", Json))!;
            Assert.True(result.Status == RunStatus.Passed, result.RunnerError);
            Assert.Equal(1, result.Result!.Value.GetProperty("passed").GetInt32());
            Assert.Contains(result.Progress!.Value.EnumerateArray(), x => x.GetProperty("kind").GetString() == "attempt");
            var attempts = (await client.GetFromJsonAsync<ResultPage>($"/api/test-runs/{run.Id}/results", Json))!;
            var attempt = Assert.Single(attempts.Items);
            Assert.Equal("passed", attempt.Status);
            Assert.Equal(run.Configuration.Cases[0].Id, attempt.CaseId);
            Assert.True(attempt.DurationMs >= 0);
            Assert.Equal(3, attempt.Artifacts.Count);
            foreach (var artifact in attempt.Artifacts)
            {
                Assert.True(artifact.Available);
                var download = await client.GetAsync($"/api/test-runs/{run.Id}/artifacts/{artifact.Id}");
                download.EnsureSuccessStatusCode();
                Assert.Equal(artifact.Size, (await download.Content.ReadAsByteArrayAsync()).LongLength);
            }
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => host.Services.GetRequiredService<ITestRunner>().ExecuteAsync(Guid.NewGuid(), result.Configuration, _ => Task.CompletedTask, cancellation.Token));
        }
        finally { serverStop.Cancel(); server.Stop(); try { await serving; } catch (OperationCanceledException) { } }
    }

    internal static async Task<RunDto> CreateRun(HttpClient client, string targetUrl = "http://127.0.0.1:5180", bool capture = false)
    {
        var project = (await (await client.PostAsJsonAsync("/api/projects", new { name = "Runner" })).Content.ReadFromJsonAsync<ProjectDto>())!;
        var suite = (await (await client.PostAsJsonAsync($"/api/projects/{project.Id}/test-suites", new { name = "Smoke" })).Content.ReadFromJsonAsync<SuiteDto>(Json))!;
        var test = (await (await client.PostAsJsonAsync($"/api/test-suites/{suite.Id}/test-cases", new { name = "Title", stableKey = "page-title" })).Content.ReadFromJsonAsync<CaseDto>(Json))!;
        var env = (await (await client.PostAsJsonAsync($"/api/projects/{project.Id}/environments", new { name = "Staging", baseUrl = targetUrl, enabled = true })).Content.ReadFromJsonAsync<EnvironmentDto>(Json))!;
        var response = await client.PostAsJsonAsync($"/api/projects/{project.Id}/test-runs", new { testSuiteId = suite.Id, environmentId = env.Id, caseIds = new[] { test.Id }, options = new { testType = "Smoke", browser = "Chromium", mode = "Headless", workers = 1, retries = 0, timeoutSeconds = 20, screenshot = capture ? "Always" : "Never", video = capture ? "Always" : "Never", trace = capture ? "Always" : "Never" } });
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;
    }
}

public sealed class RealRunnerFactAttribute : FactAttribute
{
    public RealRunnerFactAttribute() { if (Environment.GetEnvironmentVariable("QA_RUNNER_INTEGRATION") != "1") Skip = "Set QA_RUNNER_INTEGRATION=1 with Node and Chromium installed."; }
}
