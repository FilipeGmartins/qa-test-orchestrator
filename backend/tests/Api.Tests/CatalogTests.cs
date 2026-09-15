using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
using Xunit;

namespace QaTestOrchestrator.Tests;

public sealed class CatalogTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:secret@example.com")]
    [InlineData("https://example.com?token=secret")]
    [InlineData("https://example.com#secret")]
    [InlineData("not-a-url")]
    public void Rejects_invalid_environment_urls(string url) => Assert.Throws<ValidationException>(() =>
        ProjectEnvironment.Create(Guid.NewGuid(), EnvironmentName.Staging, url, false, DateTime.UtcNow));

    [Fact]
    public void Production_cannot_be_enabled_and_case_keys_cannot_be_paths()
    {
        Assert.Throws<ValidationException>(() => ProjectEnvironment.Create(Guid.NewGuid(), EnvironmentName.Production, "https://example.com", true, DateTime.UtcNow));
        Assert.Throws<ValidationException>(() => TestCase.Create(Guid.NewGuid(), "../tests/login.spec.ts", "Login", "", [], TestSuiteStatus.Active, DateTime.UtcNow));
        var item = TestCase.Create(Guid.NewGuid(), " LOGIN ", " Login ", "", ["@SMOKE"], TestSuiteStatus.Active, DateTime.UtcNow);
        Assert.Equal("login", item.StableKey);
        var version = item.Version;
        item.Edit("Login editado", "", [], TestSuiteStatus.Inactive, DateTime.UtcNow);
        Assert.Equal("login", item.StableKey);
        Assert.Equal(2, item.CatalogVersion);
        Assert.NotEqual(version, item.Version);
    }

    [Fact]
    public async Task Case_crud_filters_duplicates_immutable_key_and_stale_version()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        var (project, suite) = await Setup(client);
        var path = $"/api/test-suites/{suite.Id}/test-cases";
        var response = await client.PostAsJsonAsync(path, new { stableKey = "LOGIN", name = "Login", tags = new[] { "@SMOKE" } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<CaseDto>(Json))!;
        Assert.Equal("login", item.StableKey);
        Assert.Equal(new[] { "@smoke" }, item.Tags);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new { stableKey = "login", name = "Duplicate" })).StatusCode);
        var update = await client.PutAsJsonAsync($"/api/test-cases/{item.Id}", new { stableKey = "new-key", name = "Updated", status = "Inactive", item.Version });
        update.EnsureSuccessStatusCode();
        var saved = (await update.Content.ReadFromJsonAsync<CaseDto>(Json))!;
        Assert.Equal("login", saved.StableKey);
        Assert.Equal(2, saved.CatalogVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/test-cases/{item.Id}", new { name = "Stale", status = "Active", item.Version })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<CasePage>(path + "?status=active", Json))!.Items);
        Assert.Single((await client.GetFromJsonAsync<CasePage>(path + "?status=inactive&search=LOGIN&pageSize=1", Json))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<CasePage>(path + "?page=2&pageSize=1", Json))!.Items);
        var (_, otherSuite) = await Setup(client);
        Assert.Empty((await client.GetFromJsonAsync<CasePage>($"/api/test-suites/{otherSuite.Id}/test-cases", Json))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { stableKey = "bad-status", name = "Invalid", status = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/test-cases/{Guid.NewGuid()}")).StatusCode);
        var current = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!;
        (await client.PostAsJsonAsync($"/api/projects/{project.Id}/archive", new { current.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/test-cases/{saved.Id}", new { saved.Name, status = "Active", saved.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new { stableKey = "new", name = "New" })).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<CasePage>(path, Json))!.Items);
    }

    [Fact]
    public async Task Environments_unique_per_project_edit_version_and_archive()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        var (project, _) = await Setup(client);
        var path = $"/api/projects/{project.Id}/environments";
        var response = await client.PostAsJsonAsync(path, new { name = "Staging", baseUrl = "https://example.com", enabled = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<EnvironmentDto>(Json))!;
        Assert.True(item.Enabled);
        Assert.Equal("https://example.com/", item.BaseUrl);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new { name = "Staging", baseUrl = "https://example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { name = "Production", baseUrl = "https://example.com", enabled = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { baseUrl = "https://example.com" })).StatusCode);
        var update = await client.PutAsJsonAsync($"/api/environments/{item.Id}", new { baseUrl = "https://staging.example.com", enabled = false, item.Version });
        update.EnsureSuccessStatusCode();
        var saved = (await update.Content.ReadFromJsonAsync<EnvironmentDto>(Json))!;
        Assert.False(saved.Enabled);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/environments/{item.Id}", new { item.BaseUrl, item.Enabled, item.Version })).StatusCode);
        var (other, _) = await Setup(client);
        Assert.Empty((await client.GetFromJsonAsync<EnvironmentDto[]>($"/api/projects/{other.Id}/environments", Json))!);
        var current = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!;
        (await client.PostAsJsonAsync($"/api/projects/{project.Id}/archive", new { current.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/environments/{item.Id}", new { saved.BaseUrl, saved.Enabled, saved.Version })).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<EnvironmentDto[]>(path, Json))!);
    }

    [Fact]
    public async Task Concurrent_archive_rolls_back_catalog_changes()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        var (project, suite) = await Setup(client);
        await using var scope = host.Services.CreateAsyncScope();
        var loaded = (await scope.ServiceProvider.GetRequiredService<IProjectStore>().FindAsync(project.Id, default))!;
        (await client.PostAsJsonAsync($"/api/projects/{project.Id}/archive", new { loaded.Version })).EnsureSuccessStatusCode();
        var service = scope.ServiceProvider.GetRequiredService<CatalogService>();
        await Assert.ThrowsAsync<CatalogConflictException>(() => service.CreateCaseAsync(suite.Id, new("login", "Login", "", [], TestSuiteStatus.Active), default));
        Assert.Empty((await client.GetFromJsonAsync<CasePage>($"/api/test-suites/{suite.Id}/test-cases", Json))!.Items);
    }

    private static async Task<(ProjectDto, SuiteDto)> Setup(HttpClient client)
    {
        var project = (await (await client.PostAsJsonAsync("/api/projects", new { name = "Portal" })).Content.ReadFromJsonAsync<ProjectDto>())!;
        var suite = (await (await client.PostAsJsonAsync($"/api/projects/{project.Id}/test-suites", new { name = "Smoke" })).Content.ReadFromJsonAsync<SuiteDto>(Json))!;
        return (project, suite);
    }
}
