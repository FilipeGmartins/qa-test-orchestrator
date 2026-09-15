using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Application;

public sealed class CatalogNotFoundException() : Exception("Item do catálogo não encontrado.");
public sealed class CatalogConflictException() : Exception("O catálogo mudou ou a chave já existe. Recarregue os dados.");
public sealed record CaseRequest(string? StableKey, string? Name, string? Description, string[]? Tags, TestSuiteStatus Status = TestSuiteStatus.Active);
public sealed record CaseUpdate(string? Name, string? Description, string[]? Tags, TestSuiteStatus? Status, Guid Version);
public sealed record CaseDto(Guid Id, Guid TestSuiteId, string StableKey, string Name, string Description, string[] Tags,
    TestSuiteStatus Status, int CatalogVersion, Guid Version, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static CaseDto From(TestCase x) => new(x.Id, x.TestSuiteId, x.StableKey, x.Name, x.Description, x.Tags, x.Status, x.CatalogVersion, x.Version, x.CreatedAt, x.UpdatedAt);
}
public sealed record CasePage(IReadOnlyList<CaseDto> Items, int Total, int Page, int PageSize);
public sealed record EnvironmentRequest(EnvironmentName? Name, string? BaseUrl, bool Enabled = false);
public sealed record EnvironmentUpdate(string? BaseUrl, bool Enabled, Guid Version);
public sealed record EnvironmentDto(Guid Id, Guid ProjectId, EnvironmentName Name, string BaseUrl, bool Enabled, Guid Version, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static EnvironmentDto From(ProjectEnvironment x) => new(x.Id, x.ProjectId, x.Name, x.BaseUrl, x.Enabled, x.Version, x.CreatedAt, x.UpdatedAt);
}
public interface ICatalogStore
{
    Task<CasePage> ListCasesAsync(Guid suiteId, string search, TestSuiteStatus? status, int page, int pageSize, CancellationToken ct);
    Task<TestCase?> FindCaseAsync(Guid id, CancellationToken ct);
    Task<bool> KeyExistsAsync(Guid suiteId, string key, CancellationToken ct);
    Task AddCaseAsync(TestCase item, CancellationToken ct);
    Task<IReadOnlyList<EnvironmentDto>> ListEnvironmentsAsync(Guid projectId, CancellationToken ct);
    Task<ProjectEnvironment?> FindEnvironmentAsync(Guid id, CancellationToken ct);
    Task<bool> EnvironmentExistsAsync(Guid projectId, EnvironmentName name, CancellationToken ct);
    Task AddEnvironmentAsync(ProjectEnvironment item, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public sealed class CatalogService(ICatalogStore store, ITestSuiteStore suites, IProjectStore projects, TimeProvider clock)
{
    public async Task<CasePage> ListCasesAsync(Guid suiteId, string? search, string? status, int page, int pageSize, CancellationToken ct)
    {
        await Suite(suiteId, ct);
        if (page is < 1 or > 100000) throw new ValidationException("Página deve estar entre 1 e 100000.", "page");
        if (pageSize is < 1 or > 100) throw new ValidationException("Tamanho da página deve estar entre 1 e 100.", "pageSize");
        var term = search?.Trim() ?? "";
        if (term.Length > 120) throw new ValidationException("Busca deve ter até 120 caracteres.", "search");
        TestSuiteStatus? filter = status switch { null or "all" => null, "active" => TestSuiteStatus.Active, "inactive" => TestSuiteStatus.Inactive,
            _ => throw new ValidationException("Status deve ser active, inactive ou all.", "status") };
        return await store.ListCasesAsync(suiteId, term, filter, page, pageSize, ct);
    }
    public async Task<CaseDto> GetCaseAsync(Guid id, CancellationToken ct) => CaseDto.From(await Case(id, ct));
    public async Task<CaseDto> CreateCaseAsync(Guid suiteId, CaseRequest request, CancellationToken ct)
    {
        var suite = await Suite(suiteId, ct);
        var project = await Project(suite.ProjectId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var item = TestCase.Create(suiteId, request.StableKey, request.Name, request.Description, request.Tags, request.Status, now);
        if (await store.KeyExistsAsync(suiteId, item.StableKey, ct)) throw new CatalogConflictException();
        project.RegisterCatalogChange(now);
        await store.AddCaseAsync(item, ct);
        await store.SaveAsync(ct);
        return CaseDto.From(item);
    }
    public async Task<CaseDto> UpdateCaseAsync(Guid id, CaseUpdate request, CancellationToken ct)
    {
        var item = await Case(id, ct);
        CheckVersion(request.Version, item.Version);
        var suite = await Suite(item.TestSuiteId, ct);
        var project = await Project(suite.ProjectId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        item.Edit(request.Name, request.Description, request.Tags, request.Status ?? throw new ValidationException("Informe o status.", "status"), now);
        project.RegisterCatalogChange(now);
        await store.SaveAsync(ct);
        return CaseDto.From(item);
    }
    public async Task<IReadOnlyList<EnvironmentDto>> ListEnvironmentsAsync(Guid projectId, CancellationToken ct)
    {
        await Project(projectId, ct);
        return await store.ListEnvironmentsAsync(projectId, ct);
    }
    public async Task<EnvironmentDto> CreateEnvironmentAsync(Guid projectId, EnvironmentRequest request, CancellationToken ct)
    {
        var project = await Project(projectId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var item = ProjectEnvironment.Create(projectId, request.Name ?? throw new ValidationException("Informe o ambiente.", "name"), request.BaseUrl, request.Enabled, now);
        if (await store.EnvironmentExistsAsync(projectId, item.Name, ct)) throw new CatalogConflictException();
        project.RegisterCatalogChange(now);
        await store.AddEnvironmentAsync(item, ct);
        await store.SaveAsync(ct);
        return EnvironmentDto.From(item);
    }
    public async Task<EnvironmentDto> UpdateEnvironmentAsync(Guid id, EnvironmentUpdate request, CancellationToken ct)
    {
        var item = await store.FindEnvironmentAsync(id, ct) ?? throw new CatalogNotFoundException();
        CheckVersion(request.Version, item.Version);
        var project = await Project(item.ProjectId, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        item.Edit(request.BaseUrl, request.Enabled, now);
        project.RegisterCatalogChange(now);
        await store.SaveAsync(ct);
        return EnvironmentDto.From(item);
    }
    private async Task<TestSuite> Suite(Guid id, CancellationToken ct) => await suites.FindAsync(id, ct) ?? throw new SuiteNotFoundException();
    private async Task<TestCase> Case(Guid id, CancellationToken ct) => await store.FindCaseAsync(id, ct) ?? throw new CatalogNotFoundException();
    private async Task<Project> Project(Guid id, CancellationToken ct) => await projects.FindAsync(id, ct) ?? throw new ResourceNotFoundException();
    private static void CheckVersion(Guid sent, Guid current)
    {
        if (sent == Guid.Empty) throw new ValidationException("Informe a versão atual.", "version");
        if (sent != current) throw new CatalogConflictException();
    }
}
