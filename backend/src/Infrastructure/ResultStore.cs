using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;

namespace QaTestOrchestrator.Infrastructure;

public sealed class ResultStore(OrchestratorDbContext db, RunnerSettings settings, TimeProvider clock) : ITestResults
{
    private static readonly string[] Statuses = ["passed", "failed", "timedOut", "skipped", "interrupted"];
    private static readonly string[] Browsers = ["Chromium", "Firefox", "WebKit"];
    public async Task<ResultPage> ListAsync(Guid? runId, Guid? caseId, string? status, string? browser, int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100) throw new ValidationException("Paginação inválida.", "page");
        if (!string.IsNullOrEmpty(status) && status != "all" && !Statuses.Contains(status)) throw new ValidationException("Status inválido.", "status");
        if (!string.IsNullOrEmpty(browser) && browser != "all" && !Browsers.Contains(browser)) throw new ValidationException("Navegador inválido.", "browser");
        if (runId.HasValue && !await db.TestRuns.AnyAsync(x => x.Id == runId, ct)) throw new RunNotFoundException();
        if (caseId.HasValue && !await db.TestCases.AnyAsync(x => x.Id == caseId, ct)) throw new CatalogNotFoundException();
        var query = db.TestAttempts.AsNoTracking().AsQueryable();
        if (runId.HasValue) query = query.Where(x => x.RunId == runId);
        if (caseId.HasValue) query = query.Where(x => x.CaseId == caseId);
        if (!string.IsNullOrEmpty(status) && status != "all") query = query.Where(x => x.Status == status);
        if (!string.IsNullOrEmpty(browser) && browser != "all") query = query.Where(x => x.Browser == browser);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = items.Select(x => x.Id).ToArray();
        var artifacts = await db.TestArtifacts.AsNoTracking().Where(x => ids.Contains(x.AttemptId)).ToListAsync(ct);
        return new(items.Select(x => new AttemptDto(x.Id, x.RunId, x.CaseId, x.CaseName, x.StableKey, x.Browser,
            x.Attempt, x.Status, x.DurationMs, x.Error, x.Stack, x.Logs, x.RecordedAt,
            artifacts.Where(a => a.AttemptId == x.Id).Select(a => new ArtifactDto(a.Id, a.Kind, a.Size, a.ExpiresAt,
                a.DeletedAt is null && a.ExpiresAt > clock.GetUtcNow().UtcDateTime && SafeFile(a) is not null)).ToArray())).ToArray(), total, page, pageSize);
    }

    public async Task<(Stream Stream, string ContentType, string Name)?> DownloadAsync(Guid runId, Guid artifactId, CancellationToken ct)
    {
        var artifact = await db.TestArtifacts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == artifactId && x.RunId == runId, ct);
        if (artifact is null || artifact.DeletedAt is not null || artifact.ExpiresAt <= clock.GetUtcNow().UtcDateTime) return null;
        var file = SafeFile(artifact);
        if (file is null) return null;
        try
        {
            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (stream, "application/octet-stream", artifact.Kind + "-" + artifact.Id + Extension(artifact.Kind));
        }
        catch (IOException) { return null; }
    }

    public static string Extension(string kind) => kind switch { "screenshot" => ".png", "video" => ".webm", "trace" => ".zip", _ => throw new InvalidOperationException("Invalid artifact kind.") };
    private string? SafeFile(TestArtifact artifact)
    {
        var expected = $"evidence/{artifact.Id:D}{Extension(artifact.Kind)}";
        if (artifact.RelativePath != expected) return null;
        var root = Path.GetFullPath(settings.ArtifactsRoot);
        var path = Path.Combine(root, artifact.RunId.ToString("N"), "evidence", artifact.Id + Extension(artifact.Kind));
        if (!SafeAncestors(path, root) || !File.Exists(path)) return null;
        return path;
    }
    // Reject reparse points/junctions on every component, including configured root ancestors.
    public static bool SafeAncestors(string path, string root)
    {
        var full = Path.GetFullPath(path); var boundary = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(boundary, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return false;
        for (var current = full; current is not null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
        return true;
    }

    public async Task RecordAsync(TestRun run, JsonElement value)
    {
        var snapshot = RunDto.From(run).Configuration;
        var key = value.GetProperty("key").GetString();
        var item = snapshot.Cases.Single(x => x.StableKey == key);
        var browser = value.GetProperty("browser").GetString()!;
        var status = value.GetProperty("status").GetString()!;
        var attempt = value.GetProperty("attempt").GetInt32();
        var duration = value.GetProperty("durationMs").GetInt64();
        if (!Browsers.Contains(browser) || !Statuses.Contains(status) || attempt < 0 || attempt > snapshot.Options.Retries || duration < 0
            || snapshot.Options.Browser != BrowserType.All && snapshot.Options.Browser.ToString() != browser) throw new InvalidOperationException("Invalid attempt.");
        if (await db.TestAttempts.AnyAsync(x => x.RunId == run.Id && x.CaseId == item.Id && x.Browser == browser && x.Attempt == attempt)) return;
        var recorded = clock.GetUtcNow().UtcDateTime;
        var result = new TestAttempt { RunId = run.Id, CaseId = item.Id, CaseName = item.Name, StableKey = item.StableKey, Browser = browser,
            Status = status, Attempt = attempt, DurationMs = duration, RecordedAt = recorded,
            Error = Text(value, "error"), Stack = Text(value, "stack"), Logs = Text(value, "logs") };
        db.TestAttempts.Add(result);
        if (!value.TryGetProperty("artifacts", out var artifacts)) return;
        if (artifacts.GetArrayLength() > 10) throw new InvalidOperationException("Too many artifacts.");
        foreach (var a in artifacts.EnumerateArray())
        {
            var artifact = new TestArtifact { Id = a.GetProperty("id").GetGuid(), RunId = run.Id, AttemptId = result.Id,
                Kind = a.GetProperty("kind").GetString()!, RelativePath = a.GetProperty("relativePath").GetString()!, Size = a.GetProperty("size").GetInt64(),
                ExpiresAt = recorded.AddDays(settings.RetentionDays) };
            if (artifact.Size is < 0 or > 209715200 || SafeFile(artifact) is not { } file || new FileInfo(file).Length != artifact.Size)
                throw new InvalidOperationException("Invalid artifact.");
            db.TestArtifacts.Add(artifact);
        }
    }
    private static string Text(JsonElement value, string key) => value.TryGetProperty(key, out var text) ? Sanitize(text.GetString() ?? "") : "";
    public static string Sanitize(string value)
    {
        value = value[..Math.Min(value.Length, 16000)];
        value = Regex.Replace(value, @"\x1b\[[0-9;]*m", "");
        value = Regex.Replace(value, "https?://[^\\s<>\"']+", "[URL]", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, "(?:[a-z]:\\\\|/)(?:[^\\s<>\"']+[\\\\/])+[^\\s<>\"']*", "[PATH]", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\b(?:authorization|cookie|set-cookie)\s*[:=][^\r\n]*", "[REDACTED]", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, "\\b(?:password|passwd|secret|token|api[_-]?key)\\s*[\"']?\\s*[:=]\\s*[\"']?[^\\s,\"'}]+", "[REDACTED]", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\bBearer\s+\S+", "[REDACTED]", RegexOptions.IgnoreCase);
        return value[..Math.Min(value.Length, 4000)];
    }

    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-settings.RetentionDays);
        var runs = await db.TestRuns.Where(x => x.ArtifactsPurgedAt == null && x.FinishedAt < cutoff).OrderBy(x => x.FinishedAt).Take(20).ToListAsync(ct);
        var count = 0;
        foreach (var run in runs)
        {
            ct.ThrowIfCancellationRequested();
            var root = Path.GetFullPath(settings.ArtifactsRoot);
            var directory = Path.Combine(root, run.Id.ToString("N"));
            if (!SafeAncestors(directory, root)) continue;
            if (Directory.Exists(directory))
            {
                // Validate the complete tree before deleting; never traverse a linked directory.
                if (!SafeTree(directory, root)) continue;
                Directory.Delete(directory, recursive: true);
            }
            foreach (var artifact in await db.TestArtifacts.Where(x => x.RunId == run.Id).ToListAsync(ct)) artifact.DeletedAt = now;
            run.MarkArtifactsPurged(now);
            await db.SaveChangesAsync(ct); count++;
        }
        return count;
    }
    private static bool SafeTree(string directory, string root)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            if (!SafeAncestors(entry, root)) return false;
            if (Directory.Exists(entry) && !SafeTree(entry, root)) return false;
        }
        return true;
    }
}
