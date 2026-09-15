namespace QaTestOrchestrator.Domain;

public enum TestSuiteStatus { Active, Inactive }

public sealed class TestSuite
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string[] Tags { get; private set; } = [];
    public TestSuiteStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid Version { get; private set; }
    private TestSuite() { }

    public static TestSuite Create(Guid projectId, string? name, string? description, string[]? tags, TestSuiteStatus status, DateTime now)
    {
        if (projectId == Guid.Empty) throw new ValidationException("Informe um projeto válido.", "projectId");
        var suite = new TestSuite { Id = Guid.NewGuid(), ProjectId = projectId, CreatedAt = now };
        suite.Edit(name, description, tags, status, now);
        return suite;
    }

    public void Edit(string? name, string? description, string[]? tags, TestSuiteStatus status, DateTime now)
    {
        var metadata = CatalogMetadata.Validate(name, description, tags, status);
        Name = metadata.Name;
        Description = metadata.Description;
        Tags = metadata.Tags;
        Status = status;
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }
}
