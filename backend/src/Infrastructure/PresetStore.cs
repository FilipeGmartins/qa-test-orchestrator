using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
namespace QaTestOrchestrator.Infrastructure;

public sealed class PresetStore(OrchestratorDbContext db, TestRunService runs, TimeProvider clock) : IPresets
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private async Task<RunPreset> Find(Guid id, CancellationToken ct) => await db.RunPresets.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new PresetNotFoundException();
    private async Task<Project> Project(Guid id, CancellationToken ct) => await db.Projects.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ResourceNotFoundException();
    private static void CheckVersion(RunPreset preset, Guid version) { if (version == Guid.Empty || preset.Version != version) throw new RunConflictException(); }
    private static void CheckActive(RunPreset preset) { if (preset.Archived) throw new ValidationException("Preset arquivado não pode ser utilizado.", "preset"); }
    private static void Page(int page, int pageSize) { if (page is < 1 or > 100000 || pageSize is < 1 or > 100) throw new ValidationException("Paginação inválida.", "page"); }
    private static RunRequest Input(PresetRevision revision) => JsonSerializer.Deserialize<RunRequest>(revision.RequestJson)!;
    private static RunSnapshot Snapshot(PresetRevision revision) => JsonSerializer.Deserialize<RunSnapshot>(revision.SnapshotJson)!;
    private Task<PresetRevision> Current(RunPreset preset, CancellationToken ct) => db.PresetRevisions.SingleAsync(x => x.PresetId == preset.Id && x.Revision == preset.Revision, ct);
    private static PresetDto Dto(RunPreset preset, PresetRevision revision) => new(preset.Id, preset.ProjectId, preset.Name, preset.Description, preset.Revision, preset.Version,
        preset.Archived, preset.CreatedAt, preset.UpdatedAt, Input(revision), Snapshot(revision));
    private async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new RunConflictException(); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "PK_PresetRevisions" }) { throw new RunConflictException(); }
    }
    public async Task<PresetDto> GetAsync(Guid id, CancellationToken ct) { var preset = await Find(id, ct); return Dto(preset, await Current(preset, ct)); }
    public async Task<PresetPage> ListAsync(Guid projectId, string? search, string? status, int page, int pageSize, CancellationToken ct)
    {
        Page(page, pageSize); await Project(projectId, ct);
        search = search?.Trim() ?? ""; status ??= "active";
        if (search.Length > 120 || status is not ("active" or "archived" or "all")) throw new ValidationException("Filtro inválido.", "status");
        var query = db.RunPresets.AsNoTracking().Where(x => x.ProjectId == projectId);
        if (status != "all") query = query.Where(x => x.Archived == (status == "archived"));
        if (search.Length > 0) { var term = search.ToLowerInvariant(); query = query.Where(x => x.Name.ToLower().Contains(term)); }
        var total = await query.CountAsync(ct);
        var items = await (from p in query join r in db.PresetRevisions on new { PresetId = p.Id, p.Revision } equals new { r.PresetId, r.Revision }
                           orderby p.UpdatedAt descending, p.Id select new { Preset = p, Revision = r }).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(x => Dto(x.Preset, x.Revision)).ToArray(), total, page, pageSize);
    }
    public async Task<PresetDto> SaveAsync(Guid projectId, Guid? id, PresetWrite request, CancellationToken ct)
    {
        var project = await Project(projectId, ct);
        var preset = id.HasValue ? await Find(id.Value, ct) : null;
        if (preset is not null) { if (preset.ProjectId != projectId) throw new PresetNotFoundException(); CheckVersion(preset, request.Version); CheckActive(preset); }
        var input = request.Configuration ?? throw new ValidationException("Informe a configuração.", "configuration");
        var snapshot = await runs.PrepareAsync(projectId, input, ct);
        project.RegisterCatalogChange(Now);
        if (preset is null) { preset = RunPreset.Create(projectId, request.Name, request.Description, Now); db.RunPresets.Add(preset); }
        else preset.Revise(request.Name, request.Description, Now);
        var revision = new PresetRevision { PresetId = preset.Id, Revision = preset.Revision, Name = preset.Name, Description = preset.Description,
            RequestJson = JsonSerializer.Serialize(input with { Tags = snapshot.Tags, CaseIds = snapshot.Cases.Select(x => x.Id).ToArray() }), SnapshotJson = JsonSerializer.Serialize(snapshot), CreatedAt = Now };
        db.PresetRevisions.Add(revision); await Save(ct); return Dto(preset, revision);
    }
    public async Task<PresetDto> ArchiveAsync(Guid id, Guid version, CancellationToken ct)
    {
        var preset = await Find(id, ct); var project = await Project(preset.ProjectId, ct);
        if (!preset.Archived) { CheckVersion(preset, version); project.RegisterCatalogChange(Now); preset.Archive(Now); await Save(ct); }
        return Dto(preset, await Current(preset, ct));
    }
    private static string Fingerprint(RunSnapshot snapshot) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot))));
    public async Task<PresetPreview> PreviewAsync(Guid id, CancellationToken ct)
    {
        var preset = await Find(id, ct); CheckActive(preset);
        var snapshot = await runs.PrepareAsync(preset.ProjectId, Input(await Current(preset, ct)), ct);
        return new(preset.Version, Fingerprint(snapshot), snapshot, preset.Revision, preset.Name);
    }
    public async Task<RunDto> UseAsync(Guid id, PresetUse request, CancellationToken ct)
    {
        var preset = await Find(id, ct); CheckActive(preset); CheckVersion(preset, request.Version);
        var input = Input(await Current(preset, ct));
        var snapshot = await runs.PrepareAsync(preset.ProjectId, input, ct);
        if (request.Fingerprint != Fingerprint(snapshot)) throw new RunConflictException();
        // Fence concurrent editing/archiving of this preset in the same transaction as the new run.
        preset.Touch();
        return await runs.CreatePreparedAsync(preset.ProjectId, input, snapshot, ct, preset.Id, preset.Revision);
    }
    public async Task<PresetRevisionPage> RevisionsAsync(Guid id, int page, int pageSize, CancellationToken ct)
    {
        Page(page, pageSize); await Find(id, ct);
        var query = db.PresetRevisions.AsNoTracking().Where(x => x.PresetId == id);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Revision).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items.Select(r => new PresetRevisionDto(r.Revision, r.Name, r.Description, r.CreatedAt, Input(r), Snapshot(r))).ToArray(), total, page, pageSize);
    }
}
