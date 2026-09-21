using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
using QaTestOrchestrator.Infrastructure;
using Xunit;
namespace QaTestOrchestrator.Tests;

public sealed class PresetTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static RunRequest Input(RunDto run) => new(run.TestSuiteId, run.EnvironmentId, run.Configuration.Cases.Select(x => x.Id).ToArray(), [], run.Configuration.Options);
    private static async Task<PresetDto> Create(HttpClient client, RunDto run)
    {
        var response = await client.PostAsJsonAsync($"/api/projects/{run.ProjectId}/presets", new PresetWrite("Regression Full", "Reusable", Input(run), Guid.Empty), Json);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PresetDto>(Json))!;
    }
    [Fact]
    public async Task Revisions_preserve_configuration_and_use_creates_only_pending_with_origin()
    {
        await using var host = new ProjectTestHost(); using var client = host.CreateClient(); var source = await RunnerTests.CreateRun(client);
        var preset = await Create(client, source);
        Assert.Equal(1, preset.Revision);
        Assert.Equal(1, (await client.GetFromJsonAsync<RunPage>("/api/test-runs", Json))!.Total);
        var editedResponse = await client.PutAsJsonAsync($"/api/projects/{preset.ProjectId}/presets/{preset.Id}", new PresetWrite("Debug Login", "Revision 2", Input(source) with { Options = source.Configuration.Options with { Retries = 2 } }, preset.Version), Json);
        editedResponse.EnsureSuccessStatusCode(); var edited = (await editedResponse.Content.ReadFromJsonAsync<PresetDto>(Json))!;
        Assert.Equal(2, edited.Revision);
        var revisions = (await client.GetFromJsonAsync<PresetRevisionPage>($"/api/presets/{preset.Id}/revisions", Json))!;
        Assert.Equal(2, revisions.Total); Assert.Equal(0, revisions.Items[1].Configuration.Options!.Retries); Assert.Equal("Regression Full", revisions.Items[1].Name);
        var preview = (await client.GetFromJsonAsync<PresetPreview>($"/api/presets/{preset.Id}/preview", Json))!;
        var use = await client.PostAsJsonAsync($"/api/presets/{preset.Id}/runs", new PresetUse(preview.Version, preview.Fingerprint), Json); use.EnsureSuccessStatusCode();
        var run = (await use.Content.ReadFromJsonAsync<RunDto>(Json))!;
        Assert.Equal(RunStatus.Pending, run.Status); Assert.Equal(preset.Id, run.PresetId); Assert.Equal(2, run.PresetRevision); Assert.Equal(2, run.Configuration.Options.Retries);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/presets/{preset.Id}/runs", new PresetUse(preview.Version, preview.Fingerprint), Json)).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<RunPage>("/api/test-runs", Json))!.Total);
        var fresh = (await client.GetFromJsonAsync<PresetDto>($"/api/presets/{preset.Id}", Json))!;
        (await client.PostAsJsonAsync($"/api/presets/{preset.Id}/archive", new { fresh.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/presets/{preset.Id}/preview")).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<PresetPage>($"/api/projects/{preset.ProjectId}/presets", Json))!.Total);
        Assert.Equal(1, (await client.GetFromJsonAsync<PresetPage>($"/api/projects/{preset.ProjectId}/presets?status=archived&search=debug", Json))!.Total);
        Assert.Equal(preset.Id, (await client.GetFromJsonAsync<RunDto>($"/api/test-runs/{run.Id}", Json))!.PresetId);
    }
    [Fact]
    public async Task Changed_environment_requires_new_preview_and_inactive_case_blocks_reuse()
    {
        await using var host = new ProjectTestHost(); using var client = host.CreateClient(); var source = await RunnerTests.CreateRun(client); var preset = await Create(client, source);
        var preview = (await client.GetFromJsonAsync<PresetPreview>($"/api/presets/{preset.Id}/preview", Json))!;
        var env = (await client.GetFromJsonAsync<EnvironmentDto[]>($"/api/projects/{source.ProjectId}/environments", Json))!.Single();
        (await client.PutAsJsonAsync($"/api/environments/{env.Id}", new { name = "Staging", baseUrl = "http://127.0.0.1:5180/changed", enabled = true, env.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/presets/{preset.Id}/runs", new PresetUse(preview.Version, preview.Fingerprint), Json)).StatusCode);
        var fresh = (await client.GetFromJsonAsync<PresetPreview>($"/api/presets/{preset.Id}/preview", Json))!; Assert.Contains("changed", fresh.Configuration.BaseUrl);
        Assert.DoesNotContain("changed", (await client.GetFromJsonAsync<PresetDto>($"/api/presets/{preset.Id}", Json))!.Snapshot.BaseUrl);
        var item = (await client.GetFromJsonAsync<CaseDto>($"/api/test-cases/{source.Configuration.Cases[0].Id}", Json))!;
        (await client.PutAsJsonAsync($"/api/test-cases/{item.Id}", new { item.Name, status = "Inactive", item.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/presets/{preset.Id}/runs", new PresetUse(fresh.Version, fresh.Fingerprint), Json)).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<RunPage>("/api/test-runs", Json))!.Total);
    }
    [Fact]
    public async Task Validates_ownership_options_version_archived_project_and_production()
    {
        await using var host = new ProjectTestHost(); using var client = host.CreateClient(); var run = await RunnerTests.CreateRun(client); var other = await RunnerTests.CreateRun(client); var preset = await Create(client, run);
        async Task<HttpStatusCode> Save(RunRequest input, string name = "Valid") => (await client.PostAsJsonAsync($"/api/projects/{run.ProjectId}/presets", new PresetWrite(name, "", input, Guid.Empty), Json)).StatusCode;
        Assert.Equal(HttpStatusCode.BadRequest, await Save(Input(other))); Assert.Equal(HttpStatusCode.BadRequest, await Save(Input(run), " "));
        Assert.Equal(HttpStatusCode.BadRequest, await Save(Input(run) with { Options = run.Configuration.Options with { Workers = 11 } }));
        Assert.Equal(HttpStatusCode.BadRequest, await Save(Input(run) with { CaseIds = [] }));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/projects/{run.ProjectId}/presets/{preset.Id}", new PresetWrite("Edit", "", Input(run), Guid.NewGuid()), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/projects/{other.ProjectId}/presets/{preset.Id}", new PresetWrite("Edit", "", Input(other), preset.Version), Json)).StatusCode);
        var production = await client.PostAsJsonAsync($"/api/projects/{run.ProjectId}/environments", new { name = "Production", baseUrl = "https://example.com", enabled = false }); production.EnsureSuccessStatusCode();
        var environment = (await production.Content.ReadFromJsonAsync<EnvironmentDto>(Json))!;
        Assert.Equal(HttpStatusCode.BadRequest, await Save(Input(run) with { EnvironmentId = environment.Id }));
        var project = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{run.ProjectId}"))!;
        (await client.PostAsJsonAsync($"/api/projects/{run.ProjectId}/archive", new { project.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, await Save(Input(run)));
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync($"/api/presets/{preset.Id}/preview")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/presets/{preset.Id}/revisions")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/projects/{run.ProjectId}/presets?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/presets/{Guid.NewGuid()}")).StatusCode);
    }
    [Fact]
    public async Task Concurrent_preset_edits_are_fenced_by_version()
    {
        await using var host = new ProjectTestHost(); using var client = host.CreateClient(); var run = await RunnerTests.CreateRun(client); var preset = await Create(client, run);
        await using var a = host.Services.CreateAsyncScope(); await using var b = host.Services.CreateAsyncScope();
        var dbA = a.ServiceProvider.GetRequiredService<OrchestratorDbContext>(); var dbB = b.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
        var first = await dbA.RunPresets.SingleAsync(); var second = await dbB.RunPresets.SingleAsync();
        first.Touch(); second.Archive(DateTime.UtcNow); await dbA.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
        Assert.False((await client.GetFromJsonAsync<PresetDto>($"/api/presets/{preset.Id}", Json))!.Archived);
    }
}
