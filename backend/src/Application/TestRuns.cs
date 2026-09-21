using System.Text.Json;
using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Application;

public sealed class RunConflictException() : Exception("A execução ou o catálogo mudou. Recarregue os dados antes de continuar.");
public sealed class RunNotFoundException() : Exception("Execução não encontrada.");
public sealed record RunRequest(Guid TestSuiteId, Guid EnvironmentId, Guid[]? CaseIds, string[]? Tags, RunOptions? Options);
public sealed record RunCancelRequest(Guid Version);
public sealed record RunCaseSnapshot(Guid Id, string StableKey, string Name, int CatalogVersion, string[] Tags);
public sealed record RunSnapshot(int SchemaVersion, string ProjectName, string SuiteName, Guid SuiteVersion,
    string EnvironmentName, string BaseUrl, Guid EnvironmentVersion, RunCaseSnapshot[] Cases, string[] Tags, RunOptions Options);
public sealed record RunDto(Guid Id, Guid ProjectId, Guid TestSuiteId, Guid EnvironmentId, RunStatus Status,
    Guid Version, DateTime CreatedAt, DateTime? StartedAt, DateTime? FinishedAt, RunSnapshot Configuration, bool RunnerAvailable = false, bool CancellationRequested = false, JsonElement? Result = null, JsonElement? Progress = null, string? RunnerError = null, Guid? PresetId = null, int? PresetRevision = null)
{
    public static RunDto From(TestRun run) => new(run.Id, run.ProjectId, run.TestSuiteId, run.EnvironmentId, run.Status,
        run.Version, run.CreatedAt, run.StartedAt, run.FinishedAt, JsonSerializer.Deserialize<RunSnapshot>(run.ConfigurationSnapshot)!, false, run.CancellationRequested,
        run.ResultJson is null ? null : JsonSerializer.Deserialize<JsonElement>(run.ResultJson), JsonSerializer.Deserialize<JsonElement>(run.ProgressJson), run.RunnerError, run.PresetId, run.PresetRevision);
}
public sealed record RunPage(IReadOnlyList<RunDto> Items, int Total, int Page, int PageSize);
public interface ITestRunStore
{
    Task<TestRun?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TestCase>> CasesAsync(Guid[] ids, CancellationToken ct);
    Task<RunPage> ListAsync(Guid? projectId, RunStatus? status, int page, int pageSize, CancellationToken ct);
    Task AddAsync(TestRun run, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
public sealed class TestRunService(ITestRunStore store, IProjectStore projects, ITestSuiteStore suites, ICatalogStore catalog, TimeProvider clock, IRunnerPolicy policy)
{
    public async Task<RunDto> CreateAsync(Guid projectId, RunRequest request, CancellationToken ct)
    {
        var snapshot = await PrepareAsync(projectId, request, ct);
        return await CreatePreparedAsync(projectId, request, snapshot, ct);
    }
    public async Task<RunSnapshot> PrepareAsync(Guid projectId, RunRequest request, CancellationToken ct)
    {
        var project = await projects.FindAsync(projectId, ct) ?? throw new ResourceNotFoundException();
        if (project.ArchivedAt.HasValue) throw new ProjectArchivedException();
        var suite = await suites.FindAsync(request.TestSuiteId, ct) ?? throw new SuiteNotFoundException();
        var environment = await catalog.FindEnvironmentAsync(request.EnvironmentId, ct) ?? throw new CatalogNotFoundException();
        if (suite.ProjectId != projectId || environment.ProjectId != projectId) throw new ValidationException("Suíte e ambiente devem pertencer ao projeto.", "projectId");
        if (suite.Status != TestSuiteStatus.Active) throw new ValidationException("A suíte deve estar ativa.", "testSuiteId");
        if (!environment.Enabled || environment.Name == EnvironmentName.Production) throw new ValidationException("Selecione um ambiente habilitado fora de Production.", "environmentId");
        var options = request.Options ?? throw new ValidationException("Informe as opções de execução.", "options");
        options.Validate();
        var ids = request.CaseIds ?? [];
        if (ids.Length is < 1 or > 100 || ids.Distinct().Count() != ids.Length) throw new ValidationException("Selecione de 1 a 100 casos sem repetições.", "caseIds");
        var selected = await store.CasesAsync(ids, ct);
        if (selected.Count != ids.Length || selected.Any(x => x.TestSuiteId != suite.Id || x.Status != TestSuiteStatus.Active))
            throw new ValidationException("Todos os casos devem estar ativos e pertencer à suíte.", "caseIds");
        // Reuse the catalog's tag rules without trusting client-provided catalog metadata.
        var tags = CatalogTags.Normalize(request.Tags);
        if (tags.Length > 0 && selected.Any(x => !tags.Any(tag => x.Tags.Contains(tag))))
            throw new ValidationException("Cada caso selecionado deve conter ao menos uma das tags informadas.", "tags");
        var snapshot = new RunSnapshot(1, project.Name, suite.Name, suite.Version, environment.Name.ToString(), environment.BaseUrl, environment.Version,
            selected.OrderBy(x => x.StableKey).Select(x => new RunCaseSnapshot(x.Id, x.StableKey, x.Name, x.CatalogVersion, x.Tags)).ToArray(), tags, options);
        return snapshot;
    }
    // Only server-side callers may supply a snapshot, after PrepareAsync in the same scoped unit of work.
    public async Task<RunDto> CreatePreparedAsync(Guid projectId, RunRequest request, RunSnapshot snapshot, CancellationToken ct, Guid? presetId = null, int? presetRevision = null)
    {
        var project = await projects.FindAsync(projectId, ct) ?? throw new ResourceNotFoundException();
        var now = clock.GetUtcNow().UtcDateTime;
        // All catalog writes also update this token, detecting archive/deactivation races.
        project.RegisterCatalogChange(now);
        var run = TestRun.Create(projectId, request.TestSuiteId, request.EnvironmentId, JsonSerializer.Serialize(snapshot), now, presetId, presetRevision);
        await store.AddAsync(run, ct); await store.SaveAsync(ct);
        return RunDto.From(run) with { RunnerAvailable = policy.Enabled };
    }
    public async Task<RunDto> GetAsync(Guid id, CancellationToken ct) => RunDto.From(await Find(id, ct)) with { RunnerAvailable = policy.Enabled };
    public async Task<RunDto> CancelAsync(Guid id, RunCancelRequest request, CancellationToken ct)
    {
        var run = await Find(id, ct);
        if (request.Version == Guid.Empty) throw new ValidationException("Informe a versão atual.", "version");
        if (run.Status == RunStatus.Cancelled) return RunDto.From(run) with { RunnerAvailable = policy.Enabled };
        if (run.Version != request.Version) throw new RunConflictException();
        run.RequestCancellation(clock.GetUtcNow().UtcDateTime); await store.SaveAsync(ct);
        return RunDto.From(run) with { RunnerAvailable = policy.Enabled };
    }
    public async Task<RunPage> ListAsync(Guid? projectId, string? status, int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100) throw new ValidationException("Paginação inválida.", "page");
        RunStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (!Enum.GetNames<RunStatus>().Contains(status)) throw new ValidationException("Status inválido.", "status");
            filter = Enum.Parse<RunStatus>(status);
        }
        if (projectId.HasValue && await projects.FindAsync(projectId.Value, ct) is null) throw new ResourceNotFoundException();
        var result = await store.ListAsync(projectId, filter, page, pageSize, ct);
        return result with { Items = result.Items.Select(x => x with { RunnerAvailable = policy.Enabled }).ToArray() };
    }
    private async Task<TestRun> Find(Guid id, CancellationToken ct) => await store.FindAsync(id, ct) ?? throw new RunNotFoundException();
}
