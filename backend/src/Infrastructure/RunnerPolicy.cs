using System.Text.Json;
using Microsoft.Extensions.Configuration;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Infrastructure;
public sealed class RunnerSettings
{
    public bool Enabled { get; set; }
    public string Root { get; set; } = "";
    public string Node { get; set; } = "node";
    public string ArtifactsRoot { get; set; } = "";
    public string[] AllowedOrigins { get; set; } = [];
}
public sealed class RunnerPolicy(RunnerSettings settings) : IRunnerPolicy
{
    public bool Enabled => settings.Enabled && File.Exists(Path.Combine(settings.Root, "run.mjs"));
    public IReadOnlyList<ExecutableCase> Catalog => Enabled
        ? JsonSerializer.Deserialize<ExecutableCase[]>(File.ReadAllText(Path.Combine(settings.Root, "catalog.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))! : [];
    public void Validate(RunSnapshot snapshot)
    {
        if (!Enabled) throw new ValidationException("Runner não habilitado no servidor.", "runner");
        snapshot.Options.Validate();
        if (!Uri.TryCreate(snapshot.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || !settings.AllowedOrigins.Contains(uri.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase)
            || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new ValidationException("A origem do ambiente não foi aprovada na configuração do servidor.", "baseUrl");
        if (snapshot.EnvironmentName == "Production" || snapshot.Cases.Length is < 1 or > 100)
            throw new ValidationException("Configuração não permitida para o runner.", "configuration");
        var catalog = Catalog;
        if (snapshot.Cases.Any(x => !catalog.Any(c => c.Key == x.StableKey && c.Types.Contains(snapshot.Options.TestType.ToString()) && c.Version == 1)))
            throw new ValidationException("Há casos ou tipos sem implementação no catálogo executável. Consulte o catálogo do runner.", "caseIds");
    }
}
