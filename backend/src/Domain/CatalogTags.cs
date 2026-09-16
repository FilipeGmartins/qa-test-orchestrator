using System.Text.RegularExpressions;

namespace QaTestOrchestrator.Domain;

public static class CatalogTags
{
    public static string[] Normalize(string[]? tags)
    {
        if (tags?.Length > 20) throw new ValidationException("Informe no máximo 20 tags.", "tags");
        var cleanTags = (tags ?? []).Select(x => x?.Trim().ToLowerInvariant() ?? "").ToArray();
        if (cleanTags.Any(x => !Regex.IsMatch(x, "^@[a-z0-9][a-z0-9_-]{0,39}$", RegexOptions.CultureInvariant)))
            throw new ValidationException("Tags devem começar com @ e conter até 40 letras sem acento, números, hífen ou sublinhado.", "tags");
        return cleanTags.Distinct(StringComparer.Ordinal).ToArray();
    }
}
