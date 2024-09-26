using JobTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Data;

public class JobTrackerContext : DbContext
{
    public JobTrackerContext(DbContextOptions<JobTrackerContext> options) : base(options)
    {
    }

    public DbSet<Job> Jobs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserJob> UserJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}