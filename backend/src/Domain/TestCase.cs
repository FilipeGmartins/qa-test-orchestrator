using System.Text.RegularExpressions;

namespace QaTestOrchestrator.Domain;

public sealed class TestCase
{
    public Guid Id { get; private set; }
    public Guid TestSuiteId { get; private set; }
    public string StableKey { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string[] Tags { get; private set; } = [];
    public TestSuiteStatus Status { get; private set; }
    public int CatalogVersion { get; private set; }
    public Guid Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    private TestCase() { }

    public static TestCase Create(Guid suiteId, string? stableKey, string? name, string? description, string[]? tags, TestSuiteStatus status, DateTime now)
    {
        var key = stableKey?.Trim().ToLowerInvariant() ?? "";
        if (!Regex.IsMatch(key, "^[a-z0-9][a-z0-9_-]{0,79}$", RegexOptions.CultureInvariant))
            throw new ValidationException("A chave deve conter 1–80 letras sem acento, números, hífen ou sublinhado.", "stableKey");
        var test = new TestCase { Id = Guid.NewGuid(), TestSuiteId = suiteId, StableKey = key, CreatedAt = now };
        test.Edit(name, description, tags, status, now);
        return test;
    }

    public void Edit(string? name, string? description, string[]? tags, TestSuiteStatus status, DateTime now)
    {
        // The catalog shares the suite metadata rules; stable keys never accept executable paths.
        var metadata = CatalogMetadata.Validate(name, description, tags, status);
        Name = metadata.Name;
        Description = metadata.Description;
        Tags = metadata.Tags;
        Status = status;
        CatalogVersion++;
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }
}
