using System.Text.RegularExpressions;

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
        var cleanName = name?.Trim() ?? "";
        var cleanDescription = description?.Trim() ?? "";
        if (cleanName.Length is < 1 or > 120) throw new ValidationException("Informe um nome com 1 a 120 caracteres.", "name");
        if (cleanDescription.Length > 2000) throw new ValidationException("A descrição deve ter até 2000 caracteres.", "description");
        if (!Enum.IsDefined(status)) throw new ValidationException("Status deve ser Active ou Inactive.", "status");
        if (tags?.Length > 20) throw new ValidationException("Informe no máximo 20 tags.", "tags");
        var cleanTags = (tags ?? []).Select(x => x?.Trim().ToLowerInvariant() ?? "").ToArray();
        if (cleanTags.Any(x => !Regex.IsMatch(x, "^@[a-z0-9][a-z0-9_-]{0,39}$", RegexOptions.CultureInvariant)))
            throw new ValidationException("Tags devem começar com @ e conter até 40 letras sem acento, números, hífen ou sublinhado.", "tags");
        Name = cleanName;
        Description = cleanDescription;
        Tags = cleanTags.Distinct(StringComparer.Ordinal).ToArray();
        Status = status;
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }
}
