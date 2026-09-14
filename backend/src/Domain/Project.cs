namespace QaTestOrchestrator.Domain;

public sealed class ValidationException(string message, string field) : Exception(message)
{
    public string Field { get; } = field;
}

public sealed class ProjectArchivedException() : Exception("Projetos arquivados não podem ser editados.");

public sealed class Project
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 2000;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
    public Guid Version { get; private set; }

    private Project() { }

    public static Project Create(string? name, string? description, DateTime now)
    {
        var project = new Project { Id = Guid.NewGuid(), CreatedAt = now };
        project.SetDetails(name, description, now);
        return project;
    }

    public void Edit(string? name, string? description, DateTime now)
    {
        if (ArchivedAt.HasValue) throw new ProjectArchivedException();
        SetDetails(name, description, now);
    }

    public void Archive(DateTime now)
    {
        if (ArchivedAt.HasValue) return;
        ArchivedAt = now;
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }

    public void RegisterCatalogChange(DateTime now)
    {
        if (ArchivedAt.HasValue) throw new ProjectArchivedException();
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }

    private void SetDetails(string? name, string? description, DateTime now)
    {
        var cleanName = name?.Trim() ?? "";
        var cleanDescription = description?.Trim() ?? "";
        if (cleanName.Length is < 1 or > NameMaxLength)
            throw new ValidationException("Informe um nome com 1 a 120 caracteres.", "name");
        if (cleanDescription.Length > DescriptionMaxLength)
            throw new ValidationException("A descrição deve ter até 2000 caracteres.", "description");
        Name = cleanName;
        Description = cleanDescription;
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }
}
