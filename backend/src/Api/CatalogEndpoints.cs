using QaTestOrchestrator.Application;

namespace QaTestOrchestrator.Api;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var cases = app.MapGroup("/api/test-suites/{suiteId:guid}/test-cases").WithTags("Test Cases");
        cases.MapGet("/", (Guid suiteId, CatalogService service, CancellationToken ct, string? search = null, string? status = null, int page = 1, int pageSize = 20) =>
            service.ListCasesAsync(suiteId, search, status, page, pageSize, ct));
        cases.MapPost("/", async (Guid suiteId, CaseRequest request, CatalogService service, CancellationToken ct) =>
        {
            var item = await service.CreateCaseAsync(suiteId, request, ct);
            return Results.Created($"/api/test-cases/{item.Id}", item);
        });
        app.MapGet("/api/test-cases/{id:guid}", (Guid id, CatalogService service, CancellationToken ct) => service.GetCaseAsync(id, ct)).WithTags("Test Cases");
        app.MapPut("/api/test-cases/{id:guid}", (Guid id, CaseUpdate request, CatalogService service, CancellationToken ct) => service.UpdateCaseAsync(id, request, ct)).WithTags("Test Cases");
        var environments = app.MapGroup("/api/projects/{projectId:guid}/environments").WithTags("Environments");
        environments.MapGet("/", (Guid projectId, CatalogService service, CancellationToken ct) => service.ListEnvironmentsAsync(projectId, ct));
        environments.MapPost("/", async (Guid projectId, EnvironmentRequest request, CatalogService service, CancellationToken ct) =>
            Results.Created($"/api/projects/{projectId}/environments", await service.CreateEnvironmentAsync(projectId, request, ct)));
        app.MapPut("/api/environments/{id:guid}", (Guid id, EnvironmentUpdate request, CatalogService service, CancellationToken ct) => service.UpdateEnvironmentAsync(id, request, ct)).WithTags("Environments");
    }
}
