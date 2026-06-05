using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Problem> Problems => Set<Problem>();
    public DbSet<Contest> Contests => Set<Contest>();
    public DbSet<ContestProblem> ContestProblems => Set<ContestProblem>();
    public DbSet<Training> Trainings => Set<Training>();
    public DbSet<TrainingContest> TrainingContests => Set<TrainingContest>();
    public DbSet<UserProblemStatus> UserProblemStatuses => Set<UserProblemStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Nickname)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Names)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Surnames)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.PasswordHash)
                .IsRequired();

            entity.HasIndex(x => x.Nickname)
                .IsUnique();

            entity.HasOne(x => x.Organization)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.ShortName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => x.Name)
                .IsUnique();

            entity.HasIndex(x => x.ShortName)
                .IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.RoleName)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(x => x.User)
                .WithMany(x => x.Roles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.UserId, x.RoleName })
                .IsUnique();
        });

        modelBuilder.Entity<Problem>(entity =>
        {
            entity.ToTable("problems");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Judge)
                .IsRequired()
                .HasMaxLength(50);
        });

        modelBuilder.Entity<Contest>(entity =>
        {
            entity.ToTable("contests");

            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ContestProblem>(entity =>
        {
            entity.ToTable("contest_problems");

            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Training>(entity =>
        {
            entity.ToTable("trainings");

            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<TrainingContest>(entity =>
        {
            entity.ToTable("training_contests");

            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<UserProblemStatus>(entity =>
        {
            entity.ToTable("user_problem_statuses");

            entity.HasKey(x => x.Id);
        });
    }
}
