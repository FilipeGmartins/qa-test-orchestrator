using QaTestOrchestrator.Application;

namespace QaTestOrchestrator.Api;

public static class TestSuiteEndpoints
{
    public static void MapTestSuiteEndpoints(this WebApplication app)
    {
        var projectGroup = app.MapGroup("/api/projects/{projectId:guid}/test-suites").WithTags("Test Suites");
        projectGroup.MapGet("/", (Guid projectId, TestSuiteService service, CancellationToken ct,
            string? search = null, string? status = null, int page = 1, int pageSize = 20) => service.ListAsync(projectId, search, status, page, pageSize, ct))
            .WithName("ListTestSuites").WithSummary("Lista suítes de um projeto com busca, status e paginação.")
            .Produces<SuitePage>().Produces<ApiError>(400).Produces<ApiError>(404).Produces<ApiError>(503);
        projectGroup.MapPost("/", async (Guid projectId, SuiteRequest request, TestSuiteService service, CancellationToken ct) =>
        {
            var suite = await service.CreateAsync(projectId, request, ct);
            return Results.Created($"/api/test-suites/{suite.Id}", suite);
        }).WithName("CreateTestSuite").WithSummary("Cria suíte em projeto ativo.")
            .Produces<SuiteDto>(201).Produces<ApiError>(400).Produces<ApiError>(404).Produces<ApiError>(409).Produces<ApiError>(503);
        var group = app.MapGroup("/api/test-suites").WithTags("Test Suites");
        group.MapGet("/{id:guid}", (Guid id, TestSuiteService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithName("GetTestSuite").WithSummary("Consulta uma suíte.").Produces<SuiteDto>().Produces<ApiError>(404).Produces<ApiError>(503);
        group.MapPut("/{id:guid}", (Guid id, SuiteUpdateRequest request, TestSuiteService service, CancellationToken ct) => service.UpdateAsync(id, request, ct))
            .WithName("UpdateTestSuite").WithSummary("Edita, ativa ou inativa suíte sem excluir dados.")
            .Produces<SuiteDto>().Produces<ApiError>(400).Produces<ApiError>(404).Produces<ApiError>(409).Produces<ApiError>(503);
    }
}
