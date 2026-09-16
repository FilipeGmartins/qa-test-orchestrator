using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using QaTestOrchestrator.Application;

namespace QaTestOrchestrator.Infrastructure;
public sealed class PlaywrightTestRunner(RunnerSettings settings, IRunnerPolicy policy) : ITestRunner
{
    public async Task<string> ExecuteAsync(Guid runId, RunSnapshot snapshot, Func<string, Task> progress, CancellationToken ct)
    {
        policy.Validate(snapshot);
        var directory = Path.Combine(settings.ArtifactsRoot, runId.ToString("N"));
        Directory.CreateDirectory(directory);
        var start = new ProcessStartInfo(settings.Node) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = settings.Root };
        start.ArgumentList.Add(Path.Combine(settings.Root, "run.mjs"));
        start.Environment["QA_RUN_DIRECTORY"] = directory;
        using var process = new Process { StartInfo = start };
        process.Start();
        using var kill = ct.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        try
        {
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
            var stderr = DrainErrors(process.StandardError, ct);
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { snapshot.BaseUrl, snapshot.Cases, snapshot.Options, settings.AllowedOrigins }, jsonOptions).AsMemory(), ct);
            process.StandardInput.Close();
            string? summary = null;
            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                if (line.Length > 131072) throw new InvalidOperationException("Runner protocol limit exceeded.");
                if (!line.StartsWith('{')) continue;
                using var value = JsonDocument.Parse(line);
                if (!value.RootElement.TryGetProperty("kind", out var kind)) continue;
                if (kind.GetString() == "end") summary = line;
                if (kind.GetString() is "begin" or "attempt" or "end") await progress(line);
            }
            await process.WaitForExitAsync(ct); await stderr;
            if (summary is null || process.ExitCode is not (0 or 1)) throw new InvalidOperationException("Runner did not return a valid result.");
            using var result = JsonDocument.Parse(summary);
            var end = result.RootElement;
            var expected = snapshot.Cases.Length * (snapshot.Options.Browser == QaTestOrchestrator.Domain.BrowserType.All ? 3 : 1);
            if (end.GetProperty("total").GetInt32() != expected
                || end.GetProperty("passed").GetInt32() < 0 || end.GetProperty("failed").GetInt32() < 0 || end.GetProperty("skipped").GetInt32() < 0
                || end.GetProperty("passed").GetInt32() + end.GetProperty("failed").GetInt32() + end.GetProperty("skipped").GetInt32() != expected)
                throw new InvalidOperationException("Incomplete runner result.");
            return summary;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); }
        }
    }
    private static async Task DrainErrors(StreamReader reader, CancellationToken ct)
    {
        var buffer = new char[4096];
        while (await reader.ReadAsync(buffer.AsMemory(), ct) > 0) { } // Never persist raw stderr, URLs or secrets.
    }
}
