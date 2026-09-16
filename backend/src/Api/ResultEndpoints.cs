using QaTestOrchestrator.Application;

namespace QaTestOrchestrator.Api;
public static class ResultEndpoints
{
    public static void MapResultEndpoints(this WebApplication app)
    {
        app.MapGet("/api/test-runs/{id:guid}/results", (Guid id, ITestResults service, CancellationToken ct,
            string? status = null, string? browser = null, int page = 1, int pageSize = 12) => service.ListAsync(id, null, status, browser, page, pageSize, ct)).WithTags("Results");
        app.MapGet("/api/test-cases/{id:guid}/results", (Guid id, ITestResults service, CancellationToken ct,
            string? status = null, string? browser = null, int page = 1, int pageSize = 12) => service.ListAsync(null, id, status, browser, page, pageSize, ct)).WithTags("Results");
        app.MapGet("/api/test-runs/{id:guid}/artifacts/{artifactId:guid}", async (Guid id, Guid artifactId, ITestResults service, HttpContext context, CancellationToken ct) =>
        {
            var file = await service.DownloadAsync(id, artifactId, ct);
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return file is { } value ? Results.File(value.Stream, value.ContentType, value.Name, enableRangeProcessing: true) : Results.NotFound();
        }).WithTags("Results");
    }
}
