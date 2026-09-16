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
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TestSuite> TestSuites => Set<TestSuite>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<ProjectEnvironment> ProjectEnvironments => Set<ProjectEnvironment>();

    public DbSet<TestRun> TestRuns => Set<TestRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
        run.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        run.Property(x => x.Version).IsConcurrencyToken();
        run.Property(x => x.ConfigurationSnapshot).IsRequired();
        run.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<TestSuite>().WithMany().HasForeignKey(x => x.TestSuiteId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<ProjectEnvironment>().WithMany().HasForeignKey(x => x.EnvironmentId).OnDelete(DeleteBehavior.Restrict);
        run.HasIndex(x => new { x.ProjectId, x.CreatedAt, x.Id });
        run.HasIndex(x => new { x.Status, x.CreatedAt, x.Id });

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
            await database.Projects.AsNoTracking().AnyAsync(cancellationToken);
            await database.TestSuites.AsNoTracking().AnyAsync(cancellationToken);
            await database.TestCases.AsNoTracking().AnyAsync(cancellationToken);
            await database.ProjectEnvironments.AsNoTracking().AnyAsync(cancellationToken);
            await database.TestRuns.AsNoTracking().AnyAsync(cancellationToken);
            return true;
        }
        catch (NpgsqlException) { return false; }
    }
}
