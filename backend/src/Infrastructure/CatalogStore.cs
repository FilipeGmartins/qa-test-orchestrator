using Microsoft.EntityFrameworkCore;
using Npgsql;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Infrastructure;

public sealed class CatalogStore(OrchestratorDbContext database) : ICatalogStore
{
    public async Task<CasePage> ListCasesAsync(Guid suiteId, string search, TestSuiteStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = database.TestCases.AsNoTracking().Where(x => x.TestSuiteId == suiteId);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (search.Length > 0) { var term = search.ToLowerInvariant(); query = query.Where(x => x.Name.ToLower().Contains(term) || x.StableKey.Contains(term)); }
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(CaseDto.From).ToArray(), count, page, pageSize);
    }
    public Task<TestCase?> FindCaseAsync(Guid id, CancellationToken ct) => database.TestCases.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> KeyExistsAsync(Guid suiteId, string key, CancellationToken ct) => database.TestCases.AnyAsync(x => x.TestSuiteId == suiteId && x.StableKey == key, ct);
    public async Task AddCaseAsync(TestCase item, CancellationToken ct) => await database.TestCases.AddAsync(item, ct);
    public async Task<IReadOnlyList<EnvironmentDto>> ListEnvironmentsAsync(Guid projectId, CancellationToken ct) =>
        (await database.ProjectEnvironments.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.Name).ToListAsync(ct)).Select(EnvironmentDto.From).ToArray();
    public Task<ProjectEnvironment?> FindEnvironmentAsync(Guid id, CancellationToken ct) => database.ProjectEnvironments.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> EnvironmentExistsAsync(Guid projectId, EnvironmentName name, CancellationToken ct) => database.ProjectEnvironments.AnyAsync(x => x.ProjectId == projectId && x.Name == name, ct);
    public async Task AddEnvironmentAsync(ProjectEnvironment item, CancellationToken ct) => await database.ProjectEnvironments.AddAsync(item, ct);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await database.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new CatalogConflictException(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { throw new CatalogConflictException(); }
    }
}
