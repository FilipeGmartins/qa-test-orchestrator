using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Application;
public sealed record ExecutableCase(string Key, string Name, string[] Types, int Version);
public interface IRunnerPolicy
{
    bool Enabled { get; }
    IReadOnlyList<ExecutableCase> Catalog { get; }
    void Validate(RunSnapshot snapshot);
}
public interface ITestRunner
{
    Task<string> ExecuteAsync(Guid runId, RunSnapshot snapshot, Func<string, Task> progress, CancellationToken ct);
}
public sealed class RunQueueService(ITestRunStore store, IProjectStore projects, ITestSuiteStore suites, ICatalogStore catalog, IRunnerPolicy policy, TimeProvider clock)
{
    public async Task<RunDto> EnqueueAsync(Guid id, RunCancelRequest request, CancellationToken ct)
    {
        var run = await store.FindAsync(id, ct) ?? throw new RunNotFoundException();
        if (request.Version == Guid.Empty) throw new ValidationException("Informe a versão atual.", "version");
        if (run.Status == RunStatus.Queued) return RunDto.From(run) with { RunnerAvailable = policy.Enabled };
        if (request.Version != run.Version) throw new RunConflictException();
        if (run.Status != RunStatus.Pending) throw new RunStateException();
        var snapshot = RunDto.From(run).Configuration;
        policy.Validate(snapshot);
        var project = await projects.FindAsync(run.ProjectId, ct) ?? throw new ResourceNotFoundException();
        var suite = await suites.FindAsync(run.TestSuiteId, ct) ?? throw new SuiteNotFoundException();
        var environment = await catalog.FindEnvironmentAsync(run.EnvironmentId, ct) ?? throw new CatalogNotFoundException();
        var cases = await store.CasesAsync(snapshot.Cases.Select(x => x.Id).ToArray(), ct);
        if (suite.ProjectId != project.Id || suite.Status != TestSuiteStatus.Active || suite.Version != snapshot.SuiteVersion
            || environment.ProjectId != project.Id || !environment.Enabled || environment.Name == EnvironmentName.Production
            || environment.Version != snapshot.EnvironmentVersion || cases.Count != snapshot.Cases.Length
            || cases.Any(x => x.TestSuiteId != suite.Id || x.Status != TestSuiteStatus.Active || !snapshot.Cases.Any(y => y.Id == x.Id && y.CatalogVersion == x.CatalogVersion)))
            throw new ValidationException("O catálogo mudou. Crie uma nova configuração antes de executar.", "configuration");
        project.RegisterCatalogChange(clock.GetUtcNow().UtcDateTime);
        run.Queue(); await store.SaveAsync(ct);
        return RunDto.From(run) with { RunnerAvailable = policy.Enabled };
    }
}
