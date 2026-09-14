using QaTestOrchestrator.Application;

namespace QaTestOrchestrator.Api;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects");
        group.MapGet("/", (ProjectService service, CancellationToken ct, string? search = null,
            string? status = null, int page = 1, int pageSize = 20) => service.ListAsync(search, status, page, pageSize, ct))
            .WithName("ListProjects").WithSummary("Lista projetos com busca, status e paginação.")
            .Produces<ProjectPage>().Produces<ApiError>(400).Produces<ApiError>(503);
        group.MapGet("/{id:guid}", (Guid id, ProjectService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithName("GetProject").WithSummary("Consulta projeto, incluindo arquivados.")
            .Produces<ProjectDto>().Produces<ApiError>(404).Produces<ApiError>(503);
        group.MapPost("/", async (CreateProjectRequest request, ProjectService service, CancellationToken ct) =>
        {
            var project = await service.CreateAsync(request, ct);
            return Results.Created($"/api/projects/{project.Id}", project);
        }).WithName("CreateProject").WithSummary("Cria um projeto.")
            .Produces<ProjectDto>(201).Produces<ApiError>(400).Produces<ApiError>(503);
        group.MapPut("/{id:guid}", (Guid id, UpdateProjectRequest request, ProjectService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct)).WithName("UpdateProject").WithSummary("Edita um projeto ativo usando sua versão atual.")
            .Produces<ProjectDto>().Produces<ApiError>(400).Produces<ApiError>(404).Produces<ApiError>(409).Produces<ApiError>(503);
        group.MapPost("/{id:guid}/archive", (Guid id, ArchiveProjectRequest request, ProjectService service, CancellationToken ct) =>
            service.ArchiveAsync(id, request, ct)).WithName("ArchiveProject").WithSummary("Arquiva sem excluir o projeto ou seu histórico.")
            .Produces<ProjectDto>().Produces<ApiError>(400).Produces<ApiError>(404).Produces<ApiError>(409).Produces<ApiError>(503);
    }
}
