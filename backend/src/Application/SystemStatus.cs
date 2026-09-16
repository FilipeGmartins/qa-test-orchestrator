namespace QaTestOrchestrator.Application;

public interface IDatabaseProbe
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}

public sealed record SystemStatus(string Application, string Version, string Api, string Database);

public sealed class GetSystemStatus(IDatabaseProbe database)
{
    public async Task<SystemStatus> ExecuteAsync(CancellationToken cancellationToken) =>
        new("QA Test Orchestrator", "0.5.0", "available",
            await database.IsAvailableAsync(cancellationToken) ? "available" : "unavailable");
}
