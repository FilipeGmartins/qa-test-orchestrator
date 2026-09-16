namespace QaTestOrchestrator.Application;

public sealed record ArtifactDto(Guid Id, string Kind, long Size, DateTime ExpiresAt, bool Available);
public sealed record AttemptDto(Guid Id, Guid RunId, Guid CaseId, string CaseName, string StableKey,
    string Browser, int Attempt, string Status, long DurationMs, string Error, string Stack, string Logs,
    DateTime RecordedAt, IReadOnlyList<ArtifactDto> Artifacts);
public sealed record ResultPage(IReadOnlyList<AttemptDto> Items, int Total, int Page, int PageSize);
public interface ITestResults
{
    Task<ResultPage> ListAsync(Guid? runId, Guid? caseId, string? status, string? browser, int page, int pageSize, CancellationToken ct);
    Task<(Stream Stream, string ContentType, string Name)?> DownloadAsync(Guid runId, Guid artifactId, CancellationToken ct);
}
