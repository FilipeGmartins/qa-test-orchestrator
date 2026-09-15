namespace QaTestOrchestrator.Domain;

public enum EnvironmentName { Development, Staging, Production }

public sealed class ProjectEnvironment
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public EnvironmentName Name { get; private set; }
    public string BaseUrl { get; private set; } = "";
    public bool Enabled { get; private set; }
    public Guid Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    private ProjectEnvironment() { }

    public static ProjectEnvironment Create(Guid projectId, EnvironmentName name, string? baseUrl, bool enabled, DateTime now)
    {
        if (!Enum.IsDefined(name)) throw new ValidationException("Ambiente inválido.", "name");
        var environment = new ProjectEnvironment { Id = Guid.NewGuid(), ProjectId = projectId, Name = name, CreatedAt = now };
        environment.Edit(baseUrl, enabled, now);
        return environment;
    }

    public void Edit(string? baseUrl, bool enabled, DateTime now)
    {
        var value = baseUrl?.Trim() ?? "";
        if (value.Length > 2048 || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ValidationException("Informe uma URL HTTP(S) sem credenciais, parâmetros ou fragmentos.", "baseUrl");
        if (Name == EnvironmentName.Production && enabled)
            throw new ValidationException("Production permanece desabilitado até existir autorização específica.", "enabled");
        BaseUrl = uri.AbsoluteUri;
        Enabled = enabled;
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }
}
