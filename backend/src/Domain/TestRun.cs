namespace QaTestOrchestrator.Domain;

public enum RunStatus { Pending, Queued, Running, Passed, Failed, Error, Cancelled }
public enum TestType { Smoke, Regression, EndToEnd, API, Accessibility }
public enum BrowserType { Chromium, Firefox, WebKit, All }
public enum ExecutionMode { Headless, Headed }
public enum CapturePolicy { Always, OnFailure, Never }
public sealed class RunStateException() : Exception("A execução não permite esta transição de estado.");
public sealed record RunOptions(TestType TestType, BrowserType Browser, ExecutionMode Mode, int Workers, int Retries,
    int TimeoutSeconds, CapturePolicy Screenshot, CapturePolicy Video, CapturePolicy Trace)
{
    public void Validate()
    {
        if (!Enum.IsDefined(TestType) || !Enum.IsDefined(Browser) || !Enum.IsDefined(Mode)
            || !Enum.IsDefined(Screenshot) || !Enum.IsDefined(Video) || !Enum.IsDefined(Trace))
            throw new ValidationException("Selecione opções de execução válidas.", "options");
        if (Workers is < 1 or > 10) throw new ValidationException("Workers deve estar entre 1 e 10.", "workers");
        if (Retries is < 0 or > 5) throw new ValidationException("Retries deve estar entre 0 e 5.", "retries");
        if (TimeoutSeconds is < 5 or > 300) throw new ValidationException("Timeout deve estar entre 5 e 300 segundos.", "timeoutSeconds");
    }
}

public sealed class TestRun
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid TestSuiteId { get; private set; }
    public Guid EnvironmentId { get; private set; }
    public string ConfigurationSnapshot { get; private set; } = "";
    public RunStatus Status { get; private set; }
    public Guid Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    public bool CancellationRequested { get; private set; }
    public string ProgressJson { get; private set; } = "[]";
    public string? ResultJson { get; private set; }
    public string? RunnerError { get; private set; }
    public DateTime? ArtifactsPurgedAt { get; private set; }
    public void MarkArtifactsPurged(DateTime now) { ArtifactsPurgedAt = now; Version = Guid.NewGuid(); }
    public void Claim(Guid leaseId, DateTime now) { Start(now); LeaseId = leaseId; LeaseExpiresAt = now.AddSeconds(30); }
    public void Heartbeat(DateTime now) { if (Status != RunStatus.Running) throw new RunStateException(); LeaseExpiresAt = now.AddSeconds(30); Version = Guid.NewGuid(); }
    public void RequestCancellation(DateTime now)
    {
        if (Status == RunStatus.Running) { CancellationRequested = true; Version = Guid.NewGuid(); }
        else Cancel(now);
    }
    public void RecordProgress(string json) { if (Status != RunStatus.Running) throw new RunStateException(); ProgressJson = json; Version = Guid.NewGuid(); }
    public void Finish(string? result, string? error, RunStatus status, DateTime now)
    {
        if (CancellationRequested) Cancel(now); else Complete(status, now);
        ResultJson = result; RunnerError = error; LeaseExpiresAt = null;
    }
    private TestRun() { }
    public static TestRun Create(Guid projectId, Guid suiteId, Guid environmentId, string snapshot, DateTime now) => new()
    {
        Id = Guid.NewGuid(), ProjectId = projectId, TestSuiteId = suiteId, EnvironmentId = environmentId,
        ConfigurationSnapshot = snapshot, CreatedAt = now, Status = RunStatus.Pending, Version = Guid.NewGuid()
    };
    public void Queue() { if (Status != RunStatus.Pending) throw new RunStateException(); Status = RunStatus.Queued; Version = Guid.NewGuid(); }
    public void Start(DateTime now) { if (Status != RunStatus.Queued) throw new RunStateException(); Status = RunStatus.Running; StartedAt = now; Version = Guid.NewGuid(); }
    public void Complete(RunStatus result, DateTime now)
    {
        if (Status != RunStatus.Running || result is not (RunStatus.Passed or RunStatus.Failed or RunStatus.Error)) throw new RunStateException();
        Status = result; FinishedAt = now; Version = Guid.NewGuid();
    }
    public void Cancel(DateTime now)
    {
        if (Status == RunStatus.Cancelled) return;
        if (Status is not (RunStatus.Pending or RunStatus.Queued or RunStatus.Running)) throw new RunStateException();
        Status = RunStatus.Cancelled; FinishedAt = now; Version = Guid.NewGuid();
    }
}
