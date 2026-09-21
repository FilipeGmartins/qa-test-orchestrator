namespace QaTestOrchestrator.Domain;

public sealed class RunPreset
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public int Revision { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    public bool Archived { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    private RunPreset() { }
    public static RunPreset Create(Guid projectId, string? name, string? description, DateTime now)
    {
        var preset = new RunPreset { ProjectId = projectId, CreatedAt = now };
        preset.Revise(name, description, now); return preset;
    }
    public void Revise(string? name, string? description, DateTime now)
    {
        if (Archived) throw new ValidationException("Preset arquivado não pode ser alterado ou utilizado.", "preset");
        var metadata = CatalogMetadata.Validate(name, description, [], TestSuiteStatus.Active);
        Name = metadata.Name; Description = metadata.Description; Revision++; UpdatedAt = now; Touch();
    }
    public void Archive(DateTime now) { Archived = true; UpdatedAt = now; Touch(); }
    public void Touch() => Version = Guid.NewGuid();
}
public sealed class PresetRevision
{
    public Guid PresetId { get; set; }
    public int Revision { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string RequestJson { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
