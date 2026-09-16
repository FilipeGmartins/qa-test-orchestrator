using QaTestOrchestrator.Application;

namespace QaTestOrchestrator.Api;
public static class TestRunEndpoints
{
    public static void MapTestRunEndpoints(this WebApplication app)
    {
        app.MapPost("/api/projects/{projectId:guid}/test-runs", async (Guid projectId, RunRequest request, TestRunService service, CancellationToken ct) =>
        {
            var run = await service.CreateAsync(projectId, request, ct);
            return Results.Created($"/api/test-runs/{run.Id}", run);
        }).WithTags("Test Runs");
        var group = app.MapGroup("/api/test-runs").WithTags("Test Runs");
        group.MapGet("/", (TestRunService service, CancellationToken ct, Guid? projectId = null, string? status = null, int page = 1, int pageSize = 20) => service.ListAsync(projectId, status, page, pageSize, ct));
        group.MapGet("/{id:guid}", (Guid id, TestRunService service, CancellationToken ct) => service.GetAsync(id, ct));
        group.MapPost("/{id:guid}/cancel", (Guid id, RunCancelRequest request, TestRunService service, CancellationToken ct) => service.CancelAsync(id, request, ct));
    }
}
