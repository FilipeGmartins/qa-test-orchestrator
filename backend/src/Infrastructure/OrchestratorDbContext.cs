using Microsoft.EntityFrameworkCore;
using QaTestOrchestrator.Application;
using QaTestOrchestrator.Domain;
using Npgsql;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace QaTestOrchestrator.Infrastructure;

public sealed class OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options)
    : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<LoginSession> LoginSessions => Set<LoginSession>();
    public DbSet<AccountRegistry> AccountRegistries => Set<AccountRegistry>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TestSuite> TestSuites => Set<TestSuite>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<ProjectEnvironment> ProjectEnvironments => Set<ProjectEnvironment>();

    public DbSet<TestRun> TestRuns => Set<TestRun>();
    public DbSet<TestAttempt> TestAttempts => Set<TestAttempt>();
    public DbSet<TestArtifact> TestArtifacts => Set<TestArtifact>();
    public DbSet<RunPreset> RunPresets => Set<RunPreset>();
    public DbSet<PresetRevision> PresetRevisions => Set<PresetRevision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var account = modelBuilder.Entity<Account>();
        account.HasKey(x => x.Id);
        account.Property(x => x.Login).HasMaxLength(80);
        account.HasIndex(x => x.Login).IsUnique();
        account.Property(x => x.Name).HasMaxLength(120);
        account.Property(x => x.PasswordHash).HasMaxLength(1000);
        account.Property(x => x.Role).HasMaxLength(20);
        account.Property(x => x.Version).IsConcurrencyToken();
        account.Property(x => x.FailedAttempts).IsConcurrencyToken();
        var session = modelBuilder.Entity<LoginSession>();
        session.HasKey(x => x.Id);
        session.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        session.HasIndex(x => x.ExpiresAt);
        var registry = modelBuilder.Entity<AccountRegistry>();
        registry.HasKey(x => x.Id);
        registry.Property(x => x.Id).ValueGeneratedNever();
        registry.Property(x => x.Version).IsConcurrencyToken();
        registry.HasData(new AccountRegistry { Id = 1, Version = Guid.Empty });
        modelBuilder.Entity<TestSuite>().HasIndex(x => x.ProjectId).IsUnique().HasFilter("\"IsPageAudit\" = TRUE").HasDatabaseName("IX_TestSuites_PageAuditProject");
        var preset = modelBuilder.Entity<RunPreset>();
        preset.HasKey(x => x.Id);
        preset.Property(x => x.Name).HasMaxLength(120);
        preset.Property(x => x.Description).HasMaxLength(2000);
        preset.Property(x => x.Version).IsConcurrencyToken();
        preset.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        preset.HasIndex(x => new { x.ProjectId, x.Archived, x.UpdatedAt, x.Id });
        var revision = modelBuilder.Entity<PresetRevision>();
        revision.HasKey(x => new { x.PresetId, x.Revision });
        revision.Property(x => x.Name).HasMaxLength(120);
        revision.Property(x => x.Description).HasMaxLength(2000);
        revision.HasOne<RunPreset>().WithMany().HasForeignKey(x => x.PresetId).OnDelete(DeleteBehavior.Restrict);
        var project = modelBuilder.Entity<Project>();
        project.ToTable("Projects");
        project.HasKey(x => x.Id);
        project.Property(x => x.Name).HasMaxLength(Project.NameMaxLength).IsRequired();
        project.Property(x => x.Description).HasMaxLength(Project.DescriptionMaxLength).IsRequired();
        project.Property(x => x.Version).IsConcurrencyToken();
        project.HasIndex(x => new { x.CreatedAt, x.Id });
        project.HasIndex(x => new { x.ArchivedAt, x.CreatedAt, x.Id });
        var suite = modelBuilder.Entity<TestSuite>();
        suite.ToTable("TestSuites");
        suite.HasKey(x => x.Id);
        suite.Property(x => x.Name).HasMaxLength(120).IsRequired();
        suite.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        suite.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        suite.Property(x => x.Version).IsConcurrencyToken();
        suite.Property(x => x.Tags).HasConversion(
            tags => JsonSerializer.Serialize(tags, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<string[]>(json, (JsonSerializerOptions?)null)!)
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (left, right) => left!.SequenceEqual(right!),
                tags => tags.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag.GetHashCode())),
                tags => tags.ToArray()));
        suite.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        suite.HasIndex(x => new { x.ProjectId, x.CreatedAt, x.Id });
        var test = modelBuilder.Entity<TestCase>();
        test.ToTable("TestCases");
        test.HasKey(x => x.Id);
        test.Property(x => x.StableKey).HasMaxLength(80).IsRequired();
        test.Property(x => x.Name).HasMaxLength(120).IsRequired();
        test.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        test.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        test.Property(x => x.Version).IsConcurrencyToken();
        test.Property(x => x.Tags).HasConversion(
            tags => JsonSerializer.Serialize(tags, (JsonSerializerOptions?)null),
            json => JsonSerializer.Deserialize<string[]>(json, (JsonSerializerOptions?)null)!)
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (left, right) => left!.SequenceEqual(right!),
                tags => tags.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag.GetHashCode())),
                tags => tags.ToArray()));
        test.HasOne<TestSuite>().WithMany().HasForeignKey(x => x.TestSuiteId).OnDelete(DeleteBehavior.Restrict);
        test.HasIndex(x => new { x.TestSuiteId, x.StableKey }).IsUnique();
        test.HasIndex(x => new { x.TestSuiteId, x.CreatedAt, x.Id });
        var environment = modelBuilder.Entity<ProjectEnvironment>();
        environment.ToTable("ProjectEnvironments");
        environment.HasKey(x => x.Id);
        environment.Property(x => x.Name).HasConversion<string>().HasMaxLength(16);
        environment.Property(x => x.BaseUrl).HasMaxLength(2048).IsRequired();
        environment.Property(x => x.Version).IsConcurrencyToken();
        environment.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        environment.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
        var run = modelBuilder.Entity<TestRun>();
        run.ToTable("TestRuns");
        run.HasKey(x => x.Id);
        run.HasOne<PresetRevision>().WithMany().HasForeignKey(x => new { x.PresetId, x.PresetRevision }).OnDelete(DeleteBehavior.Restrict);
        run.Property(x => x.RunnerError).HasMaxLength(1000);
        run.HasIndex(x => new { x.Status, x.LeaseExpiresAt });
        run.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        run.Property(x => x.Version).IsConcurrencyToken();
        run.Property(x => x.ConfigurationSnapshot).IsRequired();
        run.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<TestSuite>().WithMany().HasForeignKey(x => x.TestSuiteId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<ProjectEnvironment>().WithMany().HasForeignKey(x => x.EnvironmentId).OnDelete(DeleteBehavior.Restrict);
        run.HasIndex(x => new { x.ProjectId, x.CreatedAt, x.Id });
        run.HasIndex(x => new { x.Status, x.CreatedAt, x.Id });
        run.HasIndex(x => new { x.ArtifactsPurgedAt, x.FinishedAt });
        var attempt = modelBuilder.Entity<TestAttempt>();
        attempt.HasKey(x => x.Id);
        attempt.HasOne<TestRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        attempt.HasOne<TestCase>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        attempt.HasIndex(x => new { x.RunId, x.CaseId, x.Browser, x.Attempt }).IsUnique();
        attempt.HasIndex(x => new { x.CaseId, x.RecordedAt, x.Id });
        attempt.Property(x => x.StableKey).HasMaxLength(80);
        attempt.Property(x => x.CaseName).HasMaxLength(120);
        attempt.Property(x => x.Browser).HasMaxLength(16);
        attempt.Property(x => x.Status).HasMaxLength(16);
        attempt.Property(x => x.Error).HasMaxLength(4000);
        attempt.Property(x => x.Stack).HasMaxLength(4000);
        attempt.Property(x => x.Logs).HasMaxLength(4000);
        var artifact = modelBuilder.Entity<TestArtifact>();
        artifact.HasKey(x => x.Id);
        artifact.HasOne<TestAttempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Restrict);
        artifact.HasOne<TestRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        artifact.Property(x => x.RelativePath).HasMaxLength(100);
        artifact.Property(x => x.Kind).HasMaxLength(16);

    }
}

public sealed class PostgresDatabaseProbe(OrchestratorDbContext database) : IDatabaseProbe
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        if (!await database.Database.CanConnectAsync(cancellationToken)) return false;
        try
        {
            if ((await database.Database.GetPendingMigrationsAsync(cancellationToken)).Any()) return false;
            await database.Accounts.AsNoTracking().AnyAsync(cancellationToken);
            await database.LoginSessions.AsNoTracking().AnyAsync(cancellationToken);
            await database.AccountRegistries.AsNoTracking().AnyAsync(cancellationToken);
            await database.Projects.AsNoTracking().AnyAsync(cancellationToken);
            await database.TestSuites.AsNoTracking().AnyAsync(cancellationToken);
            await database.TestCases.AsNoTracking().AnyAsync(cancellationToken);
            await database.ProjectEnvironments.AsNoTracking().AnyAsync(cancellationToken);
            await database.TestRuns.AsNoTracking().AnyAsync(cancellationToken);
            await database.TestAttempts.AsNoTracking().AnyAsync(cancellationToken);
            await database.TestArtifacts.AsNoTracking().AnyAsync(cancellationToken);
            await database.RunPresets.AsNoTracking().AnyAsync(cancellationToken);
            await database.PresetRevisions.AsNoTracking().AnyAsync(cancellationToken);
            return true;
        }
        catch (NpgsqlException) { return false; }
    }
}
