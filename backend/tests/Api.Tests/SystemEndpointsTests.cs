using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QaTestOrchestrator.Application;
using Xunit;

namespace QaTestOrchestrator.Tests;

public sealed class SystemEndpointsTests
{
    [Theory]
    [InlineData(true, HttpStatusCode.OK, "ready")]
    [InlineData(false, HttpStatusCode.ServiceUnavailable, "not_ready")]
    public async Task Readiness_reflects_database(bool available, HttpStatusCode expected, string state)
    {
        await using var factory = CreateHost(new Probe(available));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/health/ready");
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(state, (await response.Content.ReadFromJsonAsync<HealthStatus>())!.Status);
    }

    [Fact]
    public async Task Liveness_does_not_depend_on_database()
    {
        await using var factory = CreateHost(new BrokenProbe());
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health/live")).StatusCode);
    }

    [Theory]
    [InlineData(true, "available")]
    [InlineData(false, "unavailable")]
    public async Task System_reports_dependency_status(bool available, string expected)
    {
        await using var factory = CreateHost(new Probe(available));
        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<SystemStatus>("/api/system");
        Assert.Equal("available", result!.Api);
        Assert.Equal(expected, result.Database);
    }

    [Fact]
    public async Task Exceptions_have_consistent_body_without_internal_details()
    {
        await using var factory = CreateHost(new BrokenProbe());
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/system");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("INTERNAL_ERROR", body);
        Assert.DoesNotContain("private-secret", body);
        Assert.Contains("traceId", body);
    }

    [Fact]
    public async Task Openapi_contains_foundation_endpoints()
    {
        await using var factory = CreateHost(new Probe(true));
        using var client = factory.CreateClient();
        var document = await client.GetStringAsync("/openapi/v1.json");
        Assert.Contains("/api/system", document);
        Assert.Contains("/api/health/ready", document);
    }

    private static WebApplicationFactory<Program> CreateHost(IDatabaseProbe probe) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDatabaseProbe>();
                services.AddSingleton(probe);
            });
        });

    private sealed class Probe(bool available) : IDatabaseProbe
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(available);
    }

    private sealed class BrokenProbe : IDatabaseProbe
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("private-secret");
    }
}
