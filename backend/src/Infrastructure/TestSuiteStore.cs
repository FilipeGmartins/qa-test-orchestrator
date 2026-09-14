using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Infrastructure;

public sealed class TestSuiteStore(OrchestratorDbContext database, ILogger<TestSuiteStore> logger) : ITestSuiteStore
{
    public async Task<SuitePage> ListAsync(Guid projectId, string search, TestSuiteStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = database.TestSuites.AsNoTracking().Where(x => x.ProjectId == projectId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (search.Length > 0) { var term = search.ToLowerInvariant(); query = query.Where(x => x.Name.ToLower().Contains(term)); }
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(SuiteDto.From).ToArray(), count, page, pageSize);
    }
    public Task<TestSuite?> FindAsync(Guid id, CancellationToken ct) => database.TestSuites.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task AddAsync(TestSuite suite, CancellationToken ct) => await database.TestSuites.AddAsync(suite, ct);
    public async Task SaveAsync(CancellationToken ct)
    {
        var changes = database.ChangeTracker.Entries<TestSuite>().Where(x => x.State is EntityState.Added or EntityState.Modified)
            .Select(x => new { x.Entity.Id, x.Entity.ProjectId, Action = x.State == EntityState.Added ? "created" : "updated" }).ToArray();
        try { await database.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new SuiteConflictException(); }
        foreach (var change in changes) logger.LogInformation("Suite {Action}. TestSuiteId {TestSuiteId} ProjectId {ProjectId}", change.Action, change.Id, change.ProjectId);
    }
}
