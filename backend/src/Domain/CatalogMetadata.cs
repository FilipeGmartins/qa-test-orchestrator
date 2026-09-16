using System.Text.RegularExpressions;

namespace QaTestOrchestrator.Domain;

internal sealed record CatalogMetadata(string Name, string Description, string[] Tags)
{
    public static CatalogMetadata Validate(string? name, string? description, string[]? tags, TestSuiteStatus status)
    {
        var cleanName = name?.Trim() ?? "";
        var cleanDescription = description?.Trim() ?? "";
        if (cleanName.Length is < 1 or > 120) throw new ValidationException("Informe um nome com 1 a 120 caracteres.", "name");
        if (cleanDescription.Length > 2000) throw new ValidationException("A descrição deve ter até 2000 caracteres.", "description");
        if (!Enum.IsDefined(status)) throw new ValidationException("Status deve ser Active ou Inactive.", "status");
        var cleanTags = CatalogTags.Normalize(tags);
        return new(cleanName, cleanDescription, cleanTags.Distinct(StringComparer.Ordinal).ToArray());
    }
}
