using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Infrastructure;

// Each operation has a fresh DbContext; optimistic concurrency fences every claim and completion.
public sealed class RunWorker(IServiceScopeFactory scopes, ITestRunner runner, ILogger<RunWorker> logger)
{
    public async Task RunAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                if (!await ProcessNextAsync(stopping)) await Task.Delay(1000, stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Worker cycle failed ({Type}); retrying connection.", ex.GetType().Name);
                try { await Task.Delay(5000, stopping); } catch (OperationCanceledException) { break; }
            }
        }
    }

    public async Task<bool> ProcessNextAsync(CancellationToken stopping)
    {
        TestRun? claimed;
        var lease = Guid.NewGuid();
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
            var now = DateTime.UtcNow;
            // Never retry an abandoned browser automatically: it may already have caused side effects.
            var expired = await db.TestRuns.Where(x => x.Status == RunStatus.Running && x.LeaseExpiresAt < now).Take(20).ToListAsync(stopping);
            foreach (var run in expired) run.Finish(null, "Worker interrompido ou lease expirado. Crie uma nova solicitação para tentar novamente.", RunStatus.Error, now);
            try { await db.SaveChangesAsync(stopping); } catch (DbUpdateConcurrencyException) { return false; }
            claimed = await db.TestRuns.Where(x => x.Status == RunStatus.Queued).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).FirstOrDefaultAsync(stopping);
            if (claimed is null) return false;
            claimed.Claim(lease, now);
            try { await db.SaveChangesAsync(stopping); } catch (DbUpdateConcurrencyException) { return false; }
        }
        logger.LogInformation("Run claimed. RunId {RunId} LeaseId {LeaseId}", claimed.Id, lease);
        var snapshot = RunDto.From(claimed).Configuration;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        deadline.CancelAfter(TimeSpan.FromSeconds(snapshot.Options.TimeoutSeconds + 10));
        var events = new List<JsonElement>();
        string? summary = null; string? error = null; var final = RunStatus.Error;
        var execution = runner.ExecuteAsync(claimed.Id, snapshot, async json =>
        {
            if (events.Count >= 2000) throw new InvalidOperationException("Too many runner events.");
            events.Add(JsonSerializer.Deserialize<JsonElement>(json));
            // Events and heartbeat are serialized below via one gate.
            if (!await UpdateAsync(claimed.Id, lease, JsonSerializer.Serialize(events), null, null, null)) deadline.Cancel();
        }, deadline.Token);
        try
        {
            while (!execution.IsCompleted)
            {
                var tick = Task.Delay(1000, deadline.Token);
                if (await Task.WhenAny(execution, tick) == execution) break;
                deadline.Token.ThrowIfCancellationRequested();
                if (!await UpdateAsync(claimed.Id, lease, null, null, null, null)) { deadline.Cancel(); break; }
            }
            summary = await execution;
            using var document = JsonDocument.Parse(summary);
            var status = document.RootElement.GetProperty("status").GetString();
            final = status == "passed" ? RunStatus.Passed : status == "failed" ? RunStatus.Failed : RunStatus.Error;
            if (final == RunStatus.Error) error = "Falha de infraestrutura ou timeout no runner.";
        }
        catch (OperationCanceledException) { error = stopping.IsCancellationRequested ? "Worker encerrado durante a execução." : "Execução interrompida por cancelamento, timeout ou perda do lease."; }
        catch (Exception) { error = "Falha de infraestrutura no runner. Verifique Node, browsers e configuração do servidor."; }
        finally
        {
            deadline.Cancel();
            try { await execution; } catch (Exception) { }
        }
        await UpdateAsync(claimed.Id, lease, null, summary, error, final);
        return true;
    }

    private readonly SemaphoreSlim updateGate = new(1, 1);
    private async Task<bool> UpdateAsync(Guid id, Guid lease, string? progress, string? result, string? error, RunStatus? finish)
    {
        await updateGate.WaitAsync();
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OrchestratorDbContext>();
            var run = await db.TestRuns.SingleAsync(x => x.Id == id);
            if (run.Status != RunStatus.Running || run.LeaseId != lease || run.LeaseExpiresAt <= DateTime.UtcNow) return false;
            if (finish.HasValue) run.Finish(result, error, finish.Value, DateTime.UtcNow);
            else if (run.CancellationRequested) return false;
            else { run.Heartbeat(DateTime.UtcNow); if (progress is not null) run.RecordProgress(progress); }
            try { await db.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { return false; }
            return true;
        }
        finally { updateGate.Release(); }
    }
}
