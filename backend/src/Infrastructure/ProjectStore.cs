using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Infrastructure;

public sealed class ProjectStore(OrchestratorDbContext database, ILogger<ProjectStore> logger) : IProjectStore
{
    public async Task<ProjectPage> ListAsync(ProjectSearch search, CancellationToken ct)
    {
        var query = database.Projects.AsNoTracking();
        if (search.Status == ProjectFilter.Active) query = query.Where(x => x.ArchivedAt == null);
        if (search.Status == ProjectFilter.Archived) query = query.Where(x => x.ArchivedAt != null);
        if (search.Search.Length > 0)
        {
            var term = search.Search.ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((search.Page - 1) * search.PageSize).Take(search.PageSize).ToListAsync(ct);
        return new(items.Select(ProjectDto.From).ToArray(), total, search.Page, search.PageSize);
    }

    public Task<Project?> FindAsync(Guid id, CancellationToken ct) =>
        database.Projects.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task AddAsync(Project project, CancellationToken ct) => await database.Projects.AddAsync(project, ct);

    public async Task SaveAsync(CancellationToken ct)
    {
        var changes = database.ChangeTracker.Entries<Project>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified)
            .Select(x => new { x.Entity.Id, Action = x.State == EntityState.Added ? "created" : x.Entity.ArchivedAt.HasValue ? "archived" : "updated" })
            .ToArray();
        try { await database.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new RequestConflictException(); }
        foreach (var change in changes)
            logger.LogInformation("Project {Action}. ProjectId {ProjectId}", change.Action, change.Id);
    }
}
