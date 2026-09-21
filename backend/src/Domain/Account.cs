namespace QaTestOrchestrator.Domain;

public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Login { get; set; } = "";
    public string Name { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Reader";
    public bool Active { get; set; } = true;
    public Guid Version { get; set; } = Guid.NewGuid();
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class LoginSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public Guid AccountVersion { get; set; }
    public DateTime ExpiresAt { get; set; }
}
// Serializes administrative changes, including bootstrap and last-admin protection.
public sealed class AccountRegistry
{
    public int Id { get; set; } = 1;
    public Guid Version { get; set; }
}
