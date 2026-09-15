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
        if (tags?.Length > 20) throw new ValidationException("Informe no máximo 20 tags.", "tags");
        var cleanTags = (tags ?? []).Select(x => x?.Trim().ToLowerInvariant() ?? "").ToArray();
        if (cleanTags.Any(x => !Regex.IsMatch(x, "^@[a-z0-9][a-z0-9_-]{0,39}$", RegexOptions.CultureInvariant)))
            throw new ValidationException("Tags devem começar com @ e conter até 40 letras sem acento, números, hífen ou sublinhado.", "tags");
        return new(cleanName, cleanDescription, cleanTags.Distinct(StringComparer.Ordinal).ToArray());
    }
}
