using Microsoft.EntityFrameworkCore;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
namespace QaTestOrchestrator.Infrastructure;

public sealed record PageTestRequest(Guid EnvironmentId, string? Url, string[]? Checks, string[]? Devices);
public sealed class PageTestService(OrchestratorDbContext db, TestRunService runs, RunnerSettings settings, TimeProvider clock)
{
    public static readonly string[] Checks = ["load", "console", "layout"];
    public static readonly string[] Devices = ["desktop", "tablet", "mobile"];
    private static readonly Dictionary<string, string> Labels = new() { ["load"] = "Carregamento", ["console"] = "Erros JavaScript", ["layout"] = "Rolagem horizontal", ["desktop"] = "Desktop 1440×900", ["tablet"] = "Tablet 768×1024", ["mobile"] = "Celular 390×844" };
    public static Uri ValidateUrl(string? url)
    {
        if (url is null || url.Length > 2000 || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new ValidationException("Informe uma URL HTTP/HTTPS sem credenciais, parâmetros ou fragmento.", "url");
        return uri;
    }
    private static string[] Selection(string[]? values, string[] allowed, string field)
    {
        if (values is null || values.Length is < 1 or > 3 || values.Distinct().Count() != values.Length || values.Any(x => !allowed.Contains(x)))
            throw new ValidationException("Selecione de uma a três opções válidas, sem repetições.", field);
        return values;
    }
    public async Task<RunDto> Create(Guid projectId, PageTestRequest request, CancellationToken ct)
    {
        var target = ValidateUrl(request.Url);
        var checks = Selection(request.Checks, Checks, "checks"); var devices = Selection(request.Devices, Devices, "devices");
        var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == projectId, ct) ?? throw new ResourceNotFoundException();
        if (project.ArchivedAt.HasValue) throw new ProjectArchivedException();
        var environment = await db.ProjectEnvironments.SingleOrDefaultAsync(x => x.Id == request.EnvironmentId && x.ProjectId == projectId, ct) ?? throw new CatalogNotFoundException();
        if (!environment.Enabled || environment.Name == EnvironmentName.Production) throw new ValidationException("Escolha um ambiente habilitado fora de Production.", "environmentId");
        var origin = target.GetLeftPart(UriPartial.Authority);
        if (!string.Equals(origin, new Uri(environment.BaseUrl).GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)
            || !settings.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            throw new ValidationException("A URL deve pertencer ao ambiente selecionado e ter sua origem aprovada em Runner:AllowedOrigins pelo administrador do servidor.", "url");
        var now = clock.GetUtcNow().UtcDateTime;
        var suite = await db.TestSuites.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.IsPageAudit, ct);
        List<TestCase> cases;
        if (suite is null) {
            suite = TestSuite.CreatePageAudit(projectId, now); db.TestSuites.Add(suite);
            cases = (from check in Checks from device in Devices select TestCase.Create(suite.Id, $"frontend-{check}-{device}", $"{Labels[check]} · {Labels[device]}", "Verificação automática de página por URL.", ["@frontend-url"], TestSuiteStatus.Active, now)).ToList();
            db.TestCases.AddRange(cases);
        } else cases = await db.TestCases.Where(x => x.TestSuiteId == suite.Id).ToListAsync(ct);
        var keys = (from check in checks from device in devices select $"frontend-{check}-{device}").ToHashSet();
        var selected = cases.Where(x => keys.Contains(x.StableKey)).OrderBy(x => x.StableKey).ToArray();
        if (suite.Status != TestSuiteStatus.Active || selected.Length != keys.Count || selected.Any(x => x.Status != TestSuiteStatus.Active))
            throw new ValidationException("A suíte Frontend por URL ou algum dos casos selecionados está inativo. Revise o catálogo.", "checks");
        var options = new RunOptions(TestType.Smoke, BrowserType.Chromium, ExecutionMode.Headless, 1, 0, 120, CapturePolicy.Always, CapturePolicy.Never, CapturePolicy.OnFailure);
        var snapshot = new RunSnapshot(1, project.Name, suite.Name, suite.Version, environment.Name.ToString(), environment.BaseUrl, environment.Version,
            selected.Select(x => new RunCaseSnapshot(x.Id, x.StableKey, x.Name, x.CatalogVersion, x.Tags)).ToArray(), ["@frontend-url"], options, target.AbsoluteUri);
        var input = new RunRequest(suite.Id, environment.Id, selected.Select(x => x.Id).ToArray(), ["@frontend-url"], options);
        try { return await runs.CreatePreparedAsync(projectId, input, snapshot, ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "IX_TestSuites_PageAuditProject" }) { throw new RunConflictException(); }
    }
}
