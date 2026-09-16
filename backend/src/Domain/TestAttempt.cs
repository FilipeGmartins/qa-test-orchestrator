namespace QaTestOrchestrator.Domain;

public sealed class TestAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public Guid CaseId { get; set; }
    public string CaseName { get; set; } = "";
    public string StableKey { get; set; } = "";
    public string Browser { get; set; } = "";
    public int Attempt { get; set; }
    public string Status { get; set; } = "";
    public long DurationMs { get; set; }
    public string Error { get; set; } = "";
    public string Stack { get; set; } = "";
    public string Logs { get; set; } = "";
    public DateTime RecordedAt { get; set; }
}

public sealed class TestArtifact
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid RunId { get; set; }
    public string RelativePath { get; set; } = "";
    public string Kind { get; set; } = "";
    public long Size { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
