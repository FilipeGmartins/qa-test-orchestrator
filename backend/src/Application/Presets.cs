namespace QaTestOrchestrator.Application;
public sealed class PresetNotFoundException() : Exception("Preset ou revisão não encontrado.");
public sealed record PresetWrite(string? Name, string? Description, RunRequest? Configuration, Guid Version);
public sealed record PresetUse(Guid Version, string? Fingerprint);
public sealed record PresetDto(Guid Id, Guid ProjectId, string Name, string Description, int Revision, Guid Version,
    bool Archived, DateTime CreatedAt, DateTime UpdatedAt, RunRequest Configuration, RunSnapshot Snapshot);
public sealed record PresetPage(IReadOnlyList<PresetDto> Items, int Total, int Page, int PageSize);
public sealed record PresetRevisionDto(int Revision, string Name, string Description, DateTime CreatedAt, RunRequest Configuration, RunSnapshot Snapshot);
public sealed record PresetRevisionPage(IReadOnlyList<PresetRevisionDto> Items, int Total, int Page, int PageSize);
public sealed record PresetPreview(Guid Version, string Fingerprint, RunSnapshot Configuration, int Revision, string Name);
public interface IPresets
{
    Task<PresetPage> ListAsync(Guid projectId, string? search, string? status, int page, int pageSize, CancellationToken ct);
    Task<PresetDto> GetAsync(Guid id, CancellationToken ct);
    Task<PresetDto> SaveAsync(Guid projectId, Guid? id, PresetWrite request, CancellationToken ct);
    Task<PresetDto> ArchiveAsync(Guid id, Guid version, CancellationToken ct);
    Task<PresetPreview> PreviewAsync(Guid id, CancellationToken ct);
    Task<RunDto> UseAsync(Guid id, PresetUse request, CancellationToken ct);
    Task<PresetRevisionPage> RevisionsAsync(Guid id, int page, int pageSize, CancellationToken ct);
}
