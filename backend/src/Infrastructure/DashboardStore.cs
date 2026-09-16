using Microsoft.EntityFrameworkCore;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
namespace QaTestOrchestrator.Infrastructure;

public sealed class DashboardStore(OrchestratorDbContext db, TimeProvider clock) : IDashboard
{
    public async Task<DashboardDto> GetAsync(Guid? projectId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var last = to ?? today; var first = from ?? last.AddDays(-Math.Min(29, last.DayNumber));
        if (last > today || first > last || last.DayNumber - first.DayNumber >= 90)
            throw new ValidationException("Selecione um período de 1 a 90 dias, sem datas futuras.", "from");
        var start = first.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = last.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (projectId.HasValue && !await db.Projects.AnyAsync(x => x.Id == projectId, ct)) throw new ResourceNotFoundException();
        var runs = db.TestRuns.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < end);
        if (projectId.HasValue) runs = runs.Where(x => x.ProjectId == projectId);
        var counts = await runs.GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var runCounts = Enum.GetValues<RunStatus>().ToDictionary(x => x.ToString(), x => counts.FirstOrDefault(c => c.Status == x)?.Count ?? 0);
        var completed = runs.Where(x => x.Status == RunStatus.Passed || x.Status == RunStatus.Failed);
        var missing = await completed.CountAsync(x => !db.TestAttempts.Any(a => a.RunId == x.Id), ct);
        // A case/browser contributes only its last retry, and only after the run completes normally.
        var finals = from a in db.TestAttempts.AsNoTracking()
                     join run in completed on a.RunId equals run.Id
                     where !db.TestAttempts.Any(later => later.RunId == a.RunId && later.CaseId == a.CaseId && later.Browser == a.Browser && later.Attempt > a.Attempt)
                     select new { a.RunId, a.CaseId, a.CaseName, a.Browser, a.Status, a.DurationMs, a.Error, a.RecordedAt, run.CreatedAt,
                         Flaky = a.Status == "passed" && db.TestAttempts.Any(old => old.RunId == a.RunId && old.CaseId == a.CaseId && old.Browser == a.Browser && old.Attempt < a.Attempt && (old.Status == "failed" || old.Status == "timedOut")) };
        var totals = await finals.GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count(), Duration = g.Sum(x => (double)x.DurationMs) }).ToListAsync(ct);
        int Count(string status) => totals.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
        var passed = Count("passed"); var failed = Count("failed") + Count("timedOut"); var evaluated = passed + failed;
        var duration = totals.Where(x => x.Status is "passed" or "failed" or "timedOut").Sum(x => x.Duration);
        var runDays = await runs.GroupBy(x => x.CreatedAt.Date).Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync(ct);
        var testDays = await finals.GroupBy(x => new { Day = x.CreatedAt.Date, x.Status }).Select(g => new { g.Key.Day, g.Key.Status, Count = g.Count() }).ToListAsync(ct);
        var daily = Enumerable.Range(0, last.DayNumber - first.DayNumber + 1).Select(offset => {
            var date = start.AddDays(offset);
            int DayCount(string status) => testDays.FirstOrDefault(x => x.Day == date && x.Status == status)?.Count ?? 0;
            return new DashboardDay(first.AddDays(offset).ToString("yyyy-MM-dd"), runDays.FirstOrDefault(x => x.Day == date)?.Count ?? 0,
                DayCount("passed"), DayCount("failed") + DayCount("timedOut"), DayCount("skipped"));
        }).ToArray();
        var recent = await (from run in runs join project in db.Projects on run.ProjectId equals project.Id
                            join suite in db.TestSuites on run.TestSuiteId equals suite.Id
                            orderby run.CreatedAt descending, run.Id descending
                            select new DashboardRun(run.Id, project.Name, suite.Name, run.Status, run.CreatedAt)).Take(8).ToListAsync(ct);
        var failures = await finals.Where(x => x.Status == "failed" || x.Status == "timedOut").OrderByDescending(x => x.RecordedAt).ThenBy(x => x.RunId).ThenBy(x => x.CaseId).ThenBy(x => x.Browser)
            .Select(x => new DashboardFailure(x.RunId, x.CaseId, x.CaseName, x.Browser, x.Error, x.RecordedAt)).Take(8).ToListAsync(ct);
        var flakyCount = await finals.CountAsync(x => x.Flaky, ct);
        var flaky = await finals.Where(x => x.Flaky).GroupBy(x => new { x.CaseId, x.Browser })
            .Select(g => new { g.Key.CaseId, CaseName = g.Max(x => x.CaseName)!, g.Key.Browser, RecoveredRuns = g.Count() })
            .OrderByDescending(x => x.RecoveredRuns).ThenBy(x => x.CaseId).ThenBy(x => x.Browser).Take(8).ToListAsync(ct);
        return new(start, end, now, runCounts.Values.Sum(), runCounts, missing, passed, failed, Count("skipped"), Count("interrupted"),
            evaluated == 0 ? 0 : Math.Round(100.0 * passed / evaluated, 2), evaluated == 0 ? null : Math.Round(duration / evaluated, 2),
            flakyCount, daily, recent, failures, flaky.Select(x => new DashboardFlaky(x.CaseId, x.CaseName, x.Browser, x.RecoveredRuns)).ToArray());
    }
}
