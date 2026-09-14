using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
using Xunit;

namespace QaTestOrchestrator.Tests;

public sealed class TestSuiteTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Theory]
    [InlineData("smoke")]
    [InlineData("@")]
    [InlineData("@ação")]
    [InlineData("@shell;cmd")]
    public void Rejects_invalid_tags(string tag) => Assert.Throws<ValidationException>(() =>
        TestSuite.Create(Guid.NewGuid(), "Smoke", "", [tag], TestSuiteStatus.Active, DateTime.UtcNow));

    [Fact]
    public void Normalizes_tags_and_enforces_limits()
    {
        var suite = TestSuite.Create(Guid.NewGuid(), " Smoke ", " Description ", [" @SMOKE ", "@smoke", "@login"], TestSuiteStatus.Active, DateTime.UtcNow);
        Assert.Equal(new[] { "@smoke", "@login" }, suite.Tags);
        Assert.Equal("Smoke", suite.Name);
        Assert.Throws<ValidationException>(() => suite.Edit("", "", [], TestSuiteStatus.Active, DateTime.UtcNow));
        Assert.Throws<ValidationException>(() => suite.Edit("Smoke", "", [], (TestSuiteStatus)99, DateTime.UtcNow));
        Assert.Throws<ValidationException>(() => suite.Edit("Smoke", "", Enumerable.Repeat("@tag", 21).ToArray(), TestSuiteStatus.Active, DateTime.UtcNow));
    }

    [Fact]
    public async Task Create_edit_deactivate_reactivate_and_paginate_suites()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        var project = await CreateProject(client);
        var path = $"/api/projects/{project.Id}/test-suites";
        using var response = await client.PostAsJsonAsync(path, new { name = "Smoke", description = "Jornada crítica", tags = new[] { "@SMOKE", "@smoke" }, status = "Active" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var suite = (await response.Content.ReadFromJsonAsync<SuiteDto>(Json))!;
        Assert.Equal(project.Id, suite.ProjectId);
        Assert.Equal(new[] { "@smoke" }, suite.Tags);
        Assert.Equal($"/api/test-suites/{suite.Id}", response.Headers.Location!.ToString());
        Assert.NotEqual(project.Version, (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!.Version);
        using var edited = await client.PutAsJsonAsync(response.Headers.Location, new { name = "Regression", suite.Description, tags = new[] { "@regression" }, status = "Inactive", suite.Version });
        edited.EnsureSuccessStatusCode();
        var inactive = (await edited.Content.ReadFromJsonAsync<SuiteDto>(Json))!;
        Assert.Equal(TestSuiteStatus.Inactive, inactive.Status);
        Assert.Empty((await client.GetFromJsonAsync<SuitePage>(path + "?status=active", Json))!.Items);
        Assert.Single((await client.GetFromJsonAsync<SuitePage>(path + "?status=inactive&search=REGRESSION&pageSize=1", Json))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<SuitePage>(path + "?pageSize=1&page=2", Json))!.Items);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(response.Headers.Location, new { suite.Name, suite.Description, suite.Tags, status = "Active", suite.Version })).StatusCode);
        (await client.PutAsJsonAsync(response.Headers.Location, new { inactive.Name, inactive.Description, inactive.Tags, status = "Active", inactive.Version })).EnsureSuccessStatusCode();
        Assert.Single((await client.GetFromJsonAsync<SuitePage>(path + "?status=active", Json))!.Items);
    }

    [Fact]
    public async Task Archived_project_is_read_only_and_foreign_project_is_isolated()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        var project = await CreateProject(client);
        var other = await CreateProject(client);
        var path = $"/api/projects/{project.Id}/test-suites";
        var suite = (await (await client.PostAsJsonAsync(path, new { name = "Smoke", status = "Active" })).Content.ReadFromJsonAsync<SuiteDto>(Json))!;
        Assert.Empty((await client.GetFromJsonAsync<SuitePage>($"/api/projects/{other.Id}/test-suites", Json))!.Items);
        var latest = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!;
        (await client.PostAsJsonAsync($"/api/projects/{project.Id}/archive", new { latest.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new { name = "Another" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/test-suites/{suite.Id}", new { name = "Edit", status = "Active", suite.Version })).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<SuitePage>(path, Json))!.Items);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/test-suites/{suite.Id}")).StatusCode);
    }

    [Fact]
    public async Task Rejects_unknown_parent_suite_query_and_numeric_status()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/projects/{Guid.NewGuid()}/test-suites", new { name = "Smoke" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/test-suites/{Guid.NewGuid()}")).StatusCode);
        var project = await CreateProject(client);
        var path = $"/api/projects/{project.Id}/test-suites";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { name = "Smoke", status = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { name = "Smoke", status = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?status=wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_project_archive_rolls_back_suite_creation()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        var project = await CreateProject(client);
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IProjectStore>().FindAsync(project.Id, default);
        (await client.PostAsJsonAsync($"/api/projects/{project.Id}/archive", new { project.Version })).EnsureSuccessStatusCode();
        var service = scope.ServiceProvider.GetRequiredService<TestSuiteService>();
        await Assert.ThrowsAsync<SuiteConflictException>(() => service.CreateAsync(project.Id, new("Smoke", "", [], TestSuiteStatus.Active), default));
        Assert.Empty((await client.GetFromJsonAsync<SuitePage>($"/api/projects/{project.Id}/test-suites", Json))!.Items);
    }

    private static async Task<ProjectDto> CreateProject(HttpClient client) =>
        (await (await client.PostAsJsonAsync("/api/projects", new { name = "Portal" })).Content.ReadFromJsonAsync<ProjectDto>())!;
}
