using Microsoft.EntityFrameworkCore;
using QaTestOrchestrator.Api;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Infrastructure;

var workerOnly = args.Contains("--worker");
var migrateOnly = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--migrate" && arg != "--worker").ToArray());
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddOpenApi();
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddDbContext<OrchestratorDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("ConnectionStrings:Database must be configured.")));
builder.Services.AddScoped<IDatabaseProbe, PostgresDatabaseProbe>();
builder.Services.AddScoped<GetSystemStatus>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IProjectStore, ProjectStore>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<ITestSuiteStore, TestSuiteStore>();
builder.Services.AddScoped<TestSuiteService>();
builder.Services.AddScoped<ICatalogStore, CatalogStore>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<ITestRunStore, TestRunStore>();
builder.Services.AddScoped<TestRunService>();
var runnerSettings = new RunnerSettings {
    Enabled = builder.Configuration.GetValue<bool>("Runner:Enabled"),
    Root = Path.GetFullPath(builder.Configuration["Runner:Root"] ?? "../../../automation", builder.Environment.ContentRootPath),
    Node = builder.Configuration["Runner:Node"] ?? "node",
    ArtifactsRoot = Path.GetFullPath(builder.Configuration["Runner:ArtifactsRoot"] ?? "../../../.cache/run-artifacts", builder.Environment.ContentRootPath),
    AllowedOrigins = builder.Configuration.GetSection("Runner:AllowedOrigins").Get<string[]>() ?? []
};
builder.Services.AddSingleton(runnerSettings);
builder.Services.AddSingleton<IRunnerPolicy, RunnerPolicy>();
builder.Services.AddSingleton<ITestRunner, PlaywrightTestRunner>();
builder.Services.AddSingleton<RunWorker>();
builder.Services.AddScoped<RunQueueService>();

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(
    new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false)));

var app = builder.Build();
// Explicit one-shot command used by the Compose migration job; API startup never mutates the schema.
if (migrateOnly)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>().Database.MigrateAsync();
    return;
}
if (workerOnly)
{
    if (!runnerSettings.Enabled) throw new InvalidOperationException("Runner:Enabled must be true to start the worker.");
    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };
    await app.Services.GetRequiredService<RunWorker>().RunAsync(stopping.Token);
    return;
}
app.UseMiddleware<ExceptionMiddleware>();
app.MapProjectEndpoints();
app.MapTestSuiteEndpoints();
app.MapCatalogEndpoints();
app.MapTestRunEndpoints();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapGet("/api/system", (GetSystemStatus query, CancellationToken ct) => query.ExecuteAsync(ct))
    .WithName("GetSystemStatus").WithSummary("Diagnóstico da API e do banco de dados.")
    .Produces<SystemStatus>().Produces<ApiError>(500);
app.MapGet("/api/health/live", () => Results.Ok(new HealthStatus("healthy")))
    .WithName("Liveness").WithSummary("Verifica se o processo da API responde.");
app.MapGet("/api/health/ready", async (IDatabaseProbe database, CancellationToken ct) =>
    await database.IsAvailableAsync(ct)
        ? Results.Json(new HealthStatus("ready"), statusCode: 200)
        : Results.Json(new HealthStatus("not_ready"), statusCode: 503))
    .WithName("Readiness").WithSummary("Verifica conectividade com PostgreSQL.")
    .Produces<HealthStatus>(200).Produces<HealthStatus>(503);

app.Run();

public sealed record HealthStatus(string Status);
public partial class Program;
