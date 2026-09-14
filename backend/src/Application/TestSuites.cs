using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Application;

public sealed class SuiteNotFoundException() : Exception("Suíte não encontrada.");
public sealed class SuiteConflictException() : Exception("A suíte ou o projeto mudou. Recarregue os dados antes de continuar.");
public sealed record SuiteRequest(string? Name, string? Description, string[]? Tags, TestSuiteStatus Status = TestSuiteStatus.Active);
public sealed record SuiteUpdateRequest(string? Name, string? Description, string[]? Tags, TestSuiteStatus? Status, Guid Version);
public sealed record SuiteDto(Guid Id, Guid ProjectId, string Name, string Description, string[] Tags,
    TestSuiteStatus Status, DateTime CreatedAt, DateTime UpdatedAt, Guid Version)
{
    public static SuiteDto From(TestSuite suite) => new(suite.Id, suite.ProjectId, suite.Name, suite.Description,
        suite.Tags, suite.Status, suite.CreatedAt, suite.UpdatedAt, suite.Version);
}
public sealed record SuitePage(IReadOnlyList<SuiteDto> Items, int Total, int Page, int PageSize);
public interface ITestSuiteStore
{
    Task<SuitePage> ListAsync(Guid projectId, string search, TestSuiteStatus? status, int page, int pageSize, CancellationToken ct);
    Task<TestSuite?> FindAsync(Guid id, CancellationToken ct);
    Task AddAsync(TestSuite suite, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public sealed class TestSuiteService(ITestSuiteStore store, IProjectStore projects, TimeProvider clock)
{
    public async Task<SuitePage> ListAsync(Guid projectId, string? search, string? status, int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 100000) throw new ValidationException("Página deve estar entre 1 e 100000.", "page");
        if (pageSize is < 1 or > 100) throw new ValidationException("Tamanho da página deve estar entre 1 e 100.", "pageSize");
        var term = search?.Trim() ?? "";
        if (term.Length > 120) throw new ValidationException("Busca deve ter até 120 caracteres.", "search");
        TestSuiteStatus? filter = status switch
        {
            null or "all" => null,
            "active" => TestSuiteStatus.Active,
            "inactive" => TestSuiteStatus.Inactive,
            _ => throw new ValidationException("Status deve ser active, inactive ou all.", "status")
        };
        await GetProjectAsync(projectId, ct);
        return await store.ListAsync(projectId, term, filter, page, pageSize, ct);
    }

    public async Task<SuiteDto> GetAsync(Guid id, CancellationToken ct) => SuiteDto.From(await FindAsync(id, ct));

    public async Task<SuiteDto> CreateAsync(Guid projectId, SuiteRequest request, CancellationToken ct)
    {
        var project = await GetProjectAsync(projectId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var suite = TestSuite.Create(projectId, request.Name, request.Description, request.Tags, request.Status, now);
        project.RegisterCatalogChange(now);
        await store.AddAsync(suite, ct);
        await store.SaveAsync(ct);
        return SuiteDto.From(suite);
    }

    public async Task<SuiteDto> UpdateAsync(Guid id, SuiteUpdateRequest request, CancellationToken ct)
    {
        var suite = await FindAsync(id, ct);
        if (request.Version == Guid.Empty) throw new ValidationException("Informe a versão atual da suíte.", "version");
        if (request.Version != suite.Version) throw new SuiteConflictException();
        var project = await GetProjectAsync(suite.ProjectId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        project.RegisterCatalogChange(now);
        suite.Edit(request.Name, request.Description, request.Tags,
            request.Status ?? throw new ValidationException("Informe o status da suíte.", "status"), now);
        await store.SaveAsync(ct);
        return SuiteDto.From(suite);
    }

    private async Task<Project> GetProjectAsync(Guid id, CancellationToken ct) => await projects.FindAsync(id, ct) ?? throw new ResourceNotFoundException();
    private async Task<TestSuite> FindAsync(Guid id, CancellationToken ct) => await store.FindAsync(id, ct) ?? throw new SuiteNotFoundException();
}
