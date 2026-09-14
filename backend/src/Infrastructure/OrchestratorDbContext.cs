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
            return true;
        }
        catch (NpgsqlException) { return false; }
    }
}
