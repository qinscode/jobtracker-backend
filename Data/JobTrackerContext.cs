using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Data;

public class JobTrackerContext : DbContext
{
    public JobTrackerContext(DbContextOptions<JobTrackerContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Job> Jobs { get; set; }
    public DbSet<UserJob> UserJobs { get; set; }
    public DbSet<UserEmailConfig> UserEmailConfigs { get; set; }
    public DbSet<AnalyzedEmail> AnalyzedEmails { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Job configuration
        modelBuilder.Entity<Job>()
            .Property(j => j.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        modelBuilder.Entity<Job>()
            .Property(j => j.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // User configuration
        modelBuilder.Entity<User>()
            .Property(u => u.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        modelBuilder.Entity<User>()
            .Property(u => u.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // UserJob configuration
        modelBuilder.Entity<UserJob>()
            .Property(uj => uj.Status)
            .HasConversion<string>();

        modelBuilder.Entity<UserJob>()
            .Property(uj => uj.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        modelBuilder.Entity<UserJob>()
            .Property(uj => uj.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // relationships
        modelBuilder.Entity<UserJob>()
            .HasOne(uj => uj.User)
            .WithMany(u => u.UserJobs)
            .HasForeignKey(uj => uj.UserId);

        modelBuilder.Entity<UserJob>()
            .HasOne(uj => uj.Job)
            .WithMany()
            .HasForeignKey(uj => uj.JobId);

        // UserEmailConfig configuration
        modelBuilder.Entity<UserEmailConfig>()
            .Property(c => c.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        modelBuilder.Entity<UserEmailConfig>()
            .Property(c => c.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // AnalyzedEmail configuration
        modelBuilder.Entity<AnalyzedEmail>()
            .HasOne(ae => ae.UserEmailConfig)
            .WithMany()
            .HasForeignKey(ae => ae.UserEmailConfigId);

        modelBuilder.Entity<AnalyzedEmail>()
            .HasOne(ae => ae.MatchedJob)
            .WithMany()
            .HasForeignKey(ae => ae.MatchedJobId);

        // 配置 KeyPhrases 为数组类型
        modelBuilder.Entity<AnalyzedEmail>()
            .Property(e => e.KeyPhrases)
            .HasColumnType("text[]")
            .HasDefaultValue(Array.Empty<string>());

        modelBuilder.Entity<Job>()
            .Property(j => j.TechStack)
            .HasConversion(
                v => string.Join(',', v ?? Array.Empty<string>()),
                v => string.IsNullOrEmpty(v)
                    ? Array.Empty<string>()
                    : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
            );
    }
}