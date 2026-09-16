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

public sealed class DashboardTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    [Fact]
    public async Task Metrics_use_final_retry_scope_dates_and_exclude_unfinished_or_cancelled_tests()
    {
        await using var host = new ProjectTestHost(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(new Clock())));
        using var client = host.CreateClient(); var original = await RunnerTests.CreateRun(client); var other = await RunnerTests.CreateRun(client);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
            TestRun Run(int days, RunStatus state)
            {
                var run = TestRun.Create(original.ProjectId, original.TestSuiteId, original.EnvironmentId, JsonSerializer.Serialize(original.Configuration), Now.AddDays(days));
                run.Queue(); run.Start(Now.AddDays(days));
                if (state == RunStatus.Cancelled) run.Cancel(Now.AddDays(days).AddSeconds(5));
                else if (state != RunStatus.Running) run.Complete(state, Now.AddDays(days).AddSeconds(5));
                db.TestRuns.Add(run); return run;
            }
            void Attempt(TestRun run, string status, int retry = 0, long duration = 0, string browser = "Chromium") => db.TestAttempts.Add(new TestAttempt {
                RunId = run.Id, CaseId = original.Configuration.Cases[0].Id, CaseName = "Title", StableKey = "page-title", Browser = browser,
                Attempt = retry, Status = status, DurationMs = duration, RecordedAt = run.CreatedAt.AddSeconds(retry + 1), Error = status == "timedOut" ? "Timeout" : "" });
            var recovered = Run(-2, RunStatus.Passed); Attempt(recovered, "failed", duration: 9000); Attempt(recovered, "passed", 1, 2000); Attempt(recovered, "skipped", browser: "Firefox");
            Attempt(Run(-1, RunStatus.Failed), "timedOut", duration: 4000);
            Attempt(Run(0, RunStatus.Running), "failed", duration: 10000);
            Attempt(Run(0, RunStatus.Cancelled), "failed", duration: 10000);
            Run(0, RunStatus.Passed); // Old execution with no normalized details.
            Attempt(Run(-30, RunStatus.Passed), "passed", duration: 90000); // Outside default 30 UTC days.
            await db.SaveChangesAsync();
        }
        var response = await client.GetAsync($"/api/dashboard?projectId={original.ProjectId}");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var data = (await response.Content.ReadFromJsonAsync<DashboardDto>(Json))!;
        Assert.Equal(6, data.TotalRuns); Assert.Equal(1, data.Passed); Assert.Equal(1, data.Failed); Assert.Equal(1, data.Skipped);
        Assert.Equal(50, data.SuccessRate); Assert.Equal(3000, data.AverageTestDurationMs); Assert.Equal(1, data.FlakyTests);
        Assert.Equal(1, data.CompletedRunsWithoutDetails); Assert.Equal(30, data.Daily.Count);
        Assert.Equal(data.TotalRuns, data.Daily.Sum(x => x.Runs)); Assert.Equal(1, data.Daily.Sum(x => x.Passed));
        Assert.Single(data.RecentFailures); Assert.Equal("Timeout", data.RecentFailures[0].Error); Assert.Single(data.FlakyCases);
        Assert.Equal(1, data.RunCounts["Running"]); Assert.Equal(1, data.RunCounts["Cancelled"]);
        Assert.Equal(1, (await client.GetFromJsonAsync<DashboardDto>($"/api/dashboard?projectId={other.ProjectId}", Json))!.TotalRuns);
        var boundary = (await client.GetFromJsonAsync<DashboardDto>($"/api/dashboard?projectId={original.ProjectId}&from=2026-09-14&to=2026-09-14", Json))!;
        Assert.Equal(1, boundary.TotalRuns); Assert.Equal(100, boundary.SuccessRate); Assert.Single(boundary.Daily);
    }
    [Fact]
    public async Task Empty_metrics_do_not_invent_data()
    {
        await using var host = new ProjectTestHost(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(new Clock())));
        using var client = host.CreateClient(); var response = await client.GetAsync("/api/dashboard");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var data = (await response.Content.ReadFromJsonAsync<DashboardDto>(Json))!;
        Assert.Equal(0, data.TotalRuns); Assert.Equal(0, data.SuccessRate); Assert.Null(data.AverageTestDurationMs);
        Assert.Empty(data.RecentRuns); Assert.Empty(data.FlakyCases); Assert.Empty(data.RecentFailures);
        Assert.All(data.Daily, day => Assert.Equal(0, day.Runs));
    }
    [Theory]
    [InlineData("from=2026-09-16&to=2026-09-15")]
    [InlineData("from=2026-01-01&to=2026-09-16")]
    [InlineData("to=2027-01-01")]
    [InlineData("from=not-a-date")]
    public async Task Rejects_invalid_periods(string query)
    {
        await using var host = new ProjectTestHost(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(new Clock())));
        using var client = host.CreateClient(); Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/dashboard?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/dashboard?projectId={Guid.NewGuid()}")).StatusCode);
    }
}
