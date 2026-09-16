using Microsoft.EntityFrameworkCore;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Infrastructure;

public sealed class TestRunStore(OrchestratorDbContext database) : ITestRunStore
{
    public Task<TestRun?> FindAsync(Guid id, CancellationToken ct) => database.TestRuns.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<TestCase>> CasesAsync(Guid[] ids, CancellationToken ct) => await database.TestCases.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
    public async Task<RunPage> ListAsync(Guid? projectId, RunStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = database.TestRuns.AsNoTracking();
        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(RunDto.From).ToArray(), total, page, pageSize);
    }
    public async Task AddAsync(TestRun run, CancellationToken ct) => await database.TestRuns.AddAsync(run, ct);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await database.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new RunConflictException(); }
    }
}
