using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
using QaTestOrchestrator.Infrastructure;
using Xunit;

namespace QaTestOrchestrator.Tests;
public sealed class ResultTests
{
    [Fact]
    public async Task Attempts_are_idempotent_filterable_and_downloads_are_scoped_and_expire()
    {
        var settings = new RunnerSettings { ArtifactsRoot = Path.Combine(Path.GetTempPath(), "qa-results-" + Guid.NewGuid().ToString("N")) };
        await using var host = new ProjectTestHost(services => services.Replace(ServiceDescriptor.Singleton(settings)));
        using var client = host.CreateClient(); var run = await RunnerTests.CreateRun(client);
        var artifactId = Guid.NewGuid();
        var directory = Path.Combine(settings.ArtifactsRoot, run.Id.ToString("N"), "evidence");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, artifactId + ".png");
        await File.WriteAllBytesAsync(file, [137, 80, 78, 71]);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
            var entity = await db.TestRuns.SingleAsync(); entity.Queue(); entity.Claim(Guid.NewGuid(), DateTime.UtcNow.AddDays(-30));
            var store = scope.ServiceProvider.GetRequiredService<ResultStore>();
            var value = JsonSerializer.SerializeToElement(new { key = "page-title", browser = "Chromium", attempt = 0, status = "failed", durationMs = 1234,
                error = "Expected title. token=private https://user:secret@example.com?key=hidden", stack = "at C:\\work\\secret\\test.js:1", logs = "Authorization: Bearer private",
                artifacts = new[] { new { id = artifactId, kind = "screenshot", relativePath = $"evidence/{artifactId}.png", size = 4 } } });
            await store.RecordAsync(entity, value); await db.SaveChangesAsync();
            await store.RecordAsync(entity, value); await db.SaveChangesAsync();
            Assert.Equal(1, await db.TestAttempts.CountAsync());
            entity.Finish(null, null, RunStatus.Failed, DateTime.UtcNow.AddDays(-30)); await db.SaveChangesAsync();
        }
        var results = (await client.GetFromJsonAsync<ResultPage>($"/api/test-runs/{run.Id}/results?status=failed&browser=Chromium"))!;
        var attempt = Assert.Single(results.Items); Assert.Equal(1234, attempt.DurationMs);
        Assert.DoesNotContain("private", attempt.Error); Assert.DoesNotContain("secret", attempt.Stack); Assert.DoesNotContain("private", attempt.Logs);
        Assert.True(Assert.Single(attempt.Artifacts).Available);
        var download = await client.GetAsync($"/api/test-runs/{run.Id}/artifacts/{artifactId}"); download.EnsureSuccessStatusCode();
        Assert.Equal(4, (await download.Content.ReadAsByteArrayAsync()).Length);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.True(download.Headers.CacheControl!.NoStore);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/test-runs/{Guid.NewGuid()}/artifacts/{artifactId}")).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<ResultPage>($"/api/test-runs/{run.Id}/results?status=passed"))!.Total);
        Assert.Equal(1, (await client.GetFromJsonAsync<ResultPage>($"/api/test-cases/{attempt.CaseId}/results"))!.Total);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/test-runs/{run.Id}/results?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/test-runs/{run.Id}/results?browser=bad")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/test-runs/{Guid.NewGuid()}/results")).StatusCode);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>(); var artifact = await db.TestArtifacts.SingleAsync();
            artifact.RelativePath = "../../outside.png"; await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/test-runs/{run.Id}/artifacts/{artifactId}")).StatusCode);
            artifact.RelativePath = $"evidence/{artifactId}.png"; artifact.ExpiresAt = DateTime.UtcNow.AddDays(-1); await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/test-runs/{run.Id}/artifacts/{artifactId}")).StatusCode);
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ResultStore>().CleanupAsync(default));
            Assert.False(Directory.Exists(Path.GetDirectoryName(directory)));
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ResultStore>().CleanupAsync(default));
            Assert.Equal(1, await db.TestAttempts.CountAsync());
        }
        Assert.False(Assert.Single((await client.GetFromJsonAsync<ResultPage>($"/api/test-runs/{run.Id}/results"))!.Items).Artifacts[0].Available);
        Directory.Delete(settings.ArtifactsRoot);
    }

    [Fact]
    public async Task Cleanup_preserves_running_runs_and_unregistered_directories()
    {
        var settings = new RunnerSettings { ArtifactsRoot = Path.Combine(Path.GetTempPath(), "qa-results-" + Guid.NewGuid().ToString("N")) };
        await using var host = new ProjectTestHost(services => services.Replace(ServiceDescriptor.Singleton(settings)));
        using var client = host.CreateClient(); var run = await RunnerTests.CreateRun(client);
        var directory = Path.Combine(settings.ArtifactsRoot, run.Id.ToString("N")); Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "input.json"); await File.WriteAllTextAsync(file, "preserve");
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>(); var entity = await db.TestRuns.SingleAsync();
        entity.Queue(); entity.Claim(Guid.NewGuid(), DateTime.UtcNow.AddDays(-30)); await db.SaveChangesAsync();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ResultStore>().CleanupAsync(default));
        Assert.True(File.Exists(file));
        Assert.False(ResultStore.SafeAncestors(Path.Combine(settings.ArtifactsRoot, "..", "outside"), settings.ArtifactsRoot));
        File.Delete(file); Directory.Delete(directory); Directory.Delete(settings.ArtifactsRoot);
    }

    [Theory]
    [InlineData("cookie: session=secret")]
    [InlineData("{\"password\":\"secret\"}")]
    [InlineData("https://example.com/?token=secret")]
    [InlineData("at /home/secret/test.js:1")]
    public void Sanitizer_removes_known_sensitive_patterns(string text) => Assert.DoesNotContain("secret", ResultStore.Sanitize(text));
}
