using QaTestOrchestrator.Domain;
namespace QaTestOrchestrator.Application;

public sealed record DashboardDay(string Date, int Runs, int Passed, int Failed, int Skipped);
public sealed record DashboardRun(Guid Id, string ProjectName, string SuiteName, RunStatus Status, DateTime CreatedAt);
public sealed record DashboardFailure(Guid RunId, Guid CaseId, string CaseName, string Browser, string Error, DateTime RecordedAt);
public sealed record DashboardFlaky(Guid CaseId, string CaseName, string Browser, int RecoveredRuns);
public sealed record DashboardDto(DateTime From, DateTime ToExclusive, DateTime GeneratedAt, int TotalRuns,
    IReadOnlyDictionary<string, int> RunCounts, int CompletedRunsWithoutDetails, int Passed, int Failed, int Skipped,
    int Interrupted, double SuccessRate, double? AverageTestDurationMs, int FlakyTests,
    IReadOnlyList<DashboardDay> Daily, IReadOnlyList<DashboardRun> RecentRuns,
    IReadOnlyList<DashboardFailure> RecentFailures, IReadOnlyList<DashboardFlaky> FlakyCases);
public interface IDashboard
{
    Task<DashboardDto> GetAsync(Guid? projectId, DateOnly? from, DateOnly? to, CancellationToken ct);
}
