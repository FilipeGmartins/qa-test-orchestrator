using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Application;

public sealed class ResourceNotFoundException() : Exception("Projeto não encontrado.");
public sealed class RequestConflictException() : Exception("O projeto foi alterado por outra solicitação. Recarregue os dados antes de continuar.");

public sealed record ProjectDto(Guid Id, string Name, string Description, DateTime CreatedAt,
    DateTime UpdatedAt, DateTime? ArchivedAt, Guid Version)
{
    public static ProjectDto From(Project project) => new(project.Id, project.Name, project.Description,
        project.CreatedAt, project.UpdatedAt, project.ArchivedAt, project.Version);
}

public sealed record CreateProjectRequest(string? Name, string? Description);
public sealed record UpdateProjectRequest(string? Name, string? Description, Guid Version);
public sealed record ArchiveProjectRequest(Guid Version);
public sealed record ProjectPage(IReadOnlyList<ProjectDto> Items, int Total, int Page, int PageSize);
public enum ProjectFilter { Active, Archived, All }
public sealed record ProjectSearch(string Search, ProjectFilter Status, int Page, int PageSize);

// A small persistence boundary for this use case, not a generic repository.
public interface IProjectStore
{
    Task<ProjectPage> ListAsync(ProjectSearch search, CancellationToken ct);
    Task<Project?> FindAsync(Guid id, CancellationToken ct);
    Task AddAsync(Project project, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public sealed class ProjectService(IProjectStore store, TimeProvider clock)
{
    public Task<ProjectPage> ListAsync(string? search, string? status, int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 100000) throw new ValidationException("Página deve estar entre 1 e 100000.", "page");
        if (pageSize is < 1 or > 100) throw new ValidationException("Tamanho da página deve estar entre 1 e 100.", "pageSize");
        var term = search?.Trim() ?? "";
        if (term.Length > Project.NameMaxLength) throw new ValidationException("Busca deve ter até 120 caracteres.", "search");
        var filter = (status ?? "active") switch
        {
            "active" => ProjectFilter.Active,
            "archived" => ProjectFilter.Archived,
            "all" => ProjectFilter.All,
            _ => throw new ValidationException("Status deve ser active, archived ou all.", "status")
        };
        return store.ListAsync(new(term, filter, page, pageSize), ct);
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct) => ProjectDto.From(await FindAsync(id, ct));

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var project = Project.Create(request.Name, request.Description, clock.GetUtcNow().UtcDateTime);
        await store.AddAsync(project, ct);
        await store.SaveAsync(ct);
        return ProjectDto.From(project);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await FindAsync(id, ct);
        CheckVersion(project, request.Version);
        project.Edit(request.Name, request.Description, clock.GetUtcNow().UtcDateTime);
        await store.SaveAsync(ct);
        return ProjectDto.From(project);
    }

    public async Task<ProjectDto> ArchiveAsync(Guid id, ArchiveProjectRequest request, CancellationToken ct)
    {
        var project = await FindAsync(id, ct);
        if (project.ArchivedAt.HasValue) return ProjectDto.From(project);
        CheckVersion(project, request.Version);
        project.Archive(clock.GetUtcNow().UtcDateTime);
        await store.SaveAsync(ct);
        return ProjectDto.From(project);
    }

    private async Task<Project> FindAsync(Guid id, CancellationToken ct) =>
        await store.FindAsync(id, ct) ?? throw new ResourceNotFoundException();

    private static void CheckVersion(Project project, Guid version)
    {
        if (version == Guid.Empty) throw new ValidationException("Informe a versão atual do projeto.", "version");
        if (project.Version != version) throw new RequestConflictException();
    }
}
