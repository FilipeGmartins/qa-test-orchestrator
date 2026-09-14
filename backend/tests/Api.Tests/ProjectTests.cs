using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
using QaTestOrchestrator.Infrastructure;
using Xunit;

namespace QaTestOrchestrator.Tests;

public sealed class ProjectTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Project_rejects_blank_names(string? name) =>
        Assert.Throws<ValidationException>(() => Project.Create(name, null, DateTime.UtcNow));

    [Fact]
    public void Project_enforces_lengths_and_normalizes_values()
    {
        Assert.Throws<ValidationException>(() => Project.Create(new string('a', 121), null, DateTime.UtcNow));
        Assert.Throws<ValidationException>(() => Project.Create("Portal", new string('a', 2001), DateTime.UtcNow));
        var project = Project.Create(" Portal ", " descrição ", DateTime.UtcNow);
        Assert.Equal("Portal", project.Name);
        Assert.Equal("descrição", project.Description);
        Assert.Equal(120, Project.Create(new string('a', 120), new string('b', 2000), DateTime.UtcNow).Name.Length);
    }

    [Fact]
    public void Archive_is_idempotent_preserves_details_and_prevents_editing()
    {
        var now = DateTime.UtcNow;
        var project = Project.Create("Portal", "Descrição", now);
        var version = project.Version;
        project.Archive(now.AddMinutes(1));
        var archivedVersion = project.Version;
        project.Archive(now.AddMinutes(2));
        Assert.NotEqual(version, archivedVersion);
        Assert.Equal(archivedVersion, project.Version);
        Assert.Equal(now, project.CreatedAt);
        Assert.Equal(now.AddMinutes(1), project.ArchivedAt);
        Assert.Equal("Descrição", project.Description);
        Assert.Throws<ProjectArchivedException>(() => project.Edit("Novo", "", now));
    }

    [Fact]
    public async Task Http_create_read_edit_archive_preserves_record()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/projects", new { name = " Portal ", description = "Cliente" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var project = (await created.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.Equal($"/api/projects/{project.Id}", created.Headers.Location!.ToString());
        Assert.Equal("Portal", project.Name);
        var loaded = await client.GetFromJsonAsync<ProjectDto>(created.Headers.Location);
        Assert.Equal(project, loaded);

        using var edited = await client.PutAsJsonAsync(created.Headers.Location, new { name = "Portal v2", description = "Nova descrição", project.Version });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var updated = (await edited.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.NotEqual(project.Version, updated.Version);
        Assert.Equal(project.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt >= project.UpdatedAt);

        using var archived = await client.PostAsJsonAsync($"/api/projects/{project.Id}/archive", new { updated.Version });
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        var final = (await archived.Content.ReadFromJsonAsync<ProjectDto>())!;
        Assert.NotNull(final.ArchivedAt);
        Assert.Equal("Portal v2", final.Name);
        Assert.Equal("Nova descrição", final.Description);
        Assert.Empty((await client.GetFromJsonAsync<ProjectPage>("/api/projects"))!.Items);
        Assert.Single((await client.GetFromJsonAsync<ProjectPage>("/api/projects?status=archived"))!.Items);
        Assert.Equal(final, await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"));
        using var repeat = await client.PostAsJsonAsync($"/api/projects/{project.Id}/archive", new { updated.Version });
        Assert.Equal(final, await repeat.Content.ReadFromJsonAsync<ProjectDto>());
        using var forbidden = await client.PutAsJsonAsync($"/api/projects/{project.Id}", new { name = "Tentativa", final.Description, final.Version });
        Assert.Equal(HttpStatusCode.Conflict, forbidden.StatusCode);
        Assert.Contains("PROJECT_ARCHIVED", await forbidden.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_filters_pagination_and_literal_wildcards()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        foreach (var name in new[] { "Portal Alpha", "Portal Beta", "API 100%" })
            (await client.PostAsJsonAsync("/api/projects", new { name })).EnsureSuccessStatusCode();
        var first = (await client.GetFromJsonAsync<ProjectPage>("/api/projects?search=PORTAL&pageSize=1"))!;
        var second = (await client.GetFromJsonAsync<ProjectPage>("/api/projects?search=PORTAL&pageSize=1&page=2"))!;
        Assert.Equal(2, first.Total);
        Assert.Single(first.Items);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        var literal = (await client.GetFromJsonAsync<ProjectPage>("/api/projects?search=%25"))!;
        Assert.Equal("API 100%", Assert.Single(literal.Items).Name);
        Assert.Empty((await client.GetFromJsonAsync<ProjectPage>("/api/projects?page=5"))!.Items);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=100001")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?status=unknown")]
    [InlineData("?page=invalid")]
    public async Task Rejects_invalid_query_parameters(string query)
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/projects" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("traceId", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rejects_invalid_body_and_returns_not_found_consistently()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        using var invalid = await client.PostAsJsonAsync("/api/projects", new { name = " " });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("\"field\":\"name\"", await invalid.Content.ReadAsStringAsync());
        using var malformed = await client.PostAsync("/api/projects", new StringContent("{broken", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Contains("INVALID_REQUEST", await malformed.Content.ReadAsStringAsync());
        using var missing = await client.GetAsync($"/api/projects/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("PROJECT_NOT_FOUND", await missing.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Stale_and_missing_versions_do_not_overwrite_updates()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        var project = (await (await client.PostAsJsonAsync("/api/projects", new { name = "Initial" })).Content.ReadFromJsonAsync<ProjectDto>())!;
        var path = $"/api/projects/{project.Id}";
        (await client.PutAsJsonAsync(path, new { name = "Winner", project.Version })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, new { name = "Stale", project.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/archive", new { project.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new { name = "Missing version" })).StatusCode);
        Assert.Equal("Winner", (await client.GetFromJsonAsync<ProjectDto>(path))!.Name);
    }

    [Fact]
    public async Task Database_concurrency_token_rejects_two_loaded_writers()
    {
        await using var host = new ProjectTestHost();
        using var client = host.CreateClient();
        var project = (await (await client.PostAsJsonAsync("/api/projects", new { name = "Original" })).Content.ReadFromJsonAsync<ProjectDto>())!;
        await using var firstScope = host.Services.CreateAsyncScope();
        await using var secondScope = host.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IProjectStore>();
        var second = secondScope.ServiceProvider.GetRequiredService<IProjectStore>();
        var a = (await first.FindAsync(project.Id, default))!;
        var b = (await second.FindAsync(project.Id, default))!;
        a.Edit("First", "", DateTime.UtcNow);
        b.Archive(DateTime.UtcNow);
        await first.SaveAsync(default);
        await Assert.ThrowsAsync<RequestConflictException>(() => second.SaveAsync(default));
        Assert.Null((await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!.ArchivedAt);
    }
}

internal sealed class ProjectTestHost : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<OrchestratorDbContext>();
            services.RemoveAll<DbContextOptions<OrchestratorDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<OrchestratorDbContext>>();
            services.AddDbContext<OrchestratorDbContext>(options => options.UseSqlite(connection));
        });
    }

    protected override Microsoft.Extensions.Hosting.IHost CreateHost(Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>().Database.EnsureCreated();
        return host;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await connection.DisposeAsync();
    }
}
