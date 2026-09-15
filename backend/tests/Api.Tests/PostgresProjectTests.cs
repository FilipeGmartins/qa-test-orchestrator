using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Infrastructure;
using Xunit;

namespace QaTestOrchestrator.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QA_TEST_DATABASE")))
            Skip = "Defina QA_TEST_DATABASE para executar com PostgreSQL real. Um schema temporário isolado será criado e removido.";
    }
}

public sealed class PostgresProjectTests
{
    [PostgresFact]
    public async Task Migration_readiness_crud_filters_and_concurrency_on_postgres()
    {
        var connectionString = Environment.GetEnvironmentVariable("QA_TEST_DATABASE")!;
        // Only this generated schema is removed. Existing schemas and data are never reset.
        var schema = "qa_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var isolated = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema, Pooling = false }.ConnectionString;
            await using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<OrchestratorDbContext>();
                    services.RemoveAll<DbContextOptions<OrchestratorDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<OrchestratorDbContext>>();
                    services.AddDbContext<OrchestratorDbContext>(options => options.UseNpgsql(isolated));
                });
            });
            using var client = host.CreateClient();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/health/ready")).StatusCode);
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
                Assert.False(db.Database.HasPendingModelChanges());
                await db.Database.MigrateAsync();
                await db.Database.MigrateAsync(); // Reapplying is safe and has no pending changes.
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/ready")).StatusCode);
            var created = await client.PostAsJsonAsync("/api/projects", new { name = "Portal 100%", description = "PostgreSQL" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var project = (await created.Content.ReadFromJsonAsync<ProjectDto>())!;
            Assert.EndsWith("Z", (await created.Content.ReadAsStringAsync()).Split("\"createdAt\":\"")[1].Split('"')[0]);
            Assert.Single((await client.GetFromJsonAsync<ProjectPage>("/api/projects?search=PORTAL%20100%25&pageSize=1"))!.Items);
            var suiteResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/test-suites", new { name = "Smoke", tags = new[] { "@SMOKE" }, status = "Active" });
            Assert.Equal(HttpStatusCode.Created, suiteResponse.StatusCode);
            var suiteJson = System.Text.Json.JsonDocument.Parse(await suiteResponse.Content.ReadAsStringAsync());
            var suiteId = suiteJson.RootElement.GetProperty("id").GetGuid();
            var suiteRead = await client.GetStringAsync($"/api/test-suites/{suiteId}");
            Assert.Contains("@smoke", suiteRead);
            Assert.Contains("Active", suiteRead);
            var caseResponse = await client.PostAsJsonAsync($"/api/test-suites/{suiteId}/test-cases", new { stableKey = "login", name = "Login", tags = new[] { "@smoke" } });
            Assert.Equal(HttpStatusCode.Created, caseResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/test-suites/{suiteId}/test-cases", new { stableKey = "LOGIN", name = "Duplicate" })).StatusCode);
            var environmentResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/environments", new { name = "Staging", baseUrl = "https://example.com", enabled = true });
            Assert.Equal(HttpStatusCode.Created, environmentResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/projects/{project.Id}/environments", new { name = "Staging", baseUrl = "https://example.com" })).StatusCode);
            Assert.Contains("login", await client.GetStringAsync($"/api/test-suites/{suiteId}/test-cases"));

            project = (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}"))!;
            var path = $"/api/projects/{project.Id}";
            var edited = await client.PutAsJsonAsync(path, new { name = "Portal editado", project.Description, project.Version });
            edited.EnsureSuccessStatusCode();
            var updated = (await edited.Content.ReadFromJsonAsync<ProjectDto>())!;
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/archive", new { project.Version })).StatusCode);
            (await client.PostAsJsonAsync(path + "/archive", new { updated.Version })).EnsureSuccessStatusCode();
            Assert.Empty((await client.GetFromJsonAsync<ProjectPage>("/api/projects"))!.Items);
            Assert.Single((await client.GetFromJsonAsync<ProjectPage>("/api/projects?status=archived"))!.Items);
            Assert.NotNull((await client.GetFromJsonAsync<ProjectDto>(path))!.ArchivedAt);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
