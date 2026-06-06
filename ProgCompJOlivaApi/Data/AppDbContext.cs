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
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<CodeforcesGym> CodeforcesGyms => Set<CodeforcesGym>();

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

            // PostgreSQL text[] for free-form search keywords.
            entity.Property(x => x.Keywords)
                .HasColumnType("text[]");

            // Speeds up the common filters used by task search.
            entity.HasIndex(x => x.Judge);
            entity.HasIndex(x => x.Difficulty);
            entity.HasIndex(x => x.ExternalId);

            entity.HasMany(x => x.Topics)
                .WithMany(x => x.Problems)
                .UsingEntity(join => join.ToTable("problem_topics"));
        });

        modelBuilder.Entity<Topic>(entity =>
        {
            entity.ToTable("topics");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => x.Name)
                .IsUnique();
        });

        modelBuilder.Entity<Contest>(entity =>
        {
            entity.ToTable("contests");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(x => x.Name)
                .IsUnique();
        });

        modelBuilder.Entity<ContestProblem>(entity =>
        {
            entity.ToTable("contest_problems");

            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Contest)
                .WithMany(x => x.ContestProblems)
                .HasForeignKey(x => x.ContestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Problem)
                .WithMany()
                .HasForeignKey(x => x.ProblemId)
                .OnDelete(DeleteBehavior.Restrict);

            // A problem appears at most once per contest.
            entity.HasIndex(x => new { x.ContestId, x.ProblemId })
                .IsUnique();
        });

        modelBuilder.Entity<Training>(entity =>
        {
            entity.ToTable("trainings");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Slug)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(x => x.Name)
                .IsUnique();

            entity.HasIndex(x => x.Slug)
                .IsUnique();
        });

        modelBuilder.Entity<TrainingContest>(entity =>
        {
            entity.ToTable("training_contests");

            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.Training)
                .WithMany(x => x.TrainingContests)
                .HasForeignKey(x => x.TrainingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Contest)
                .WithMany()
                .HasForeignKey(x => x.ContestId)
                .OnDelete(DeleteBehavior.Restrict);

            // A contest appears at most once per training.
            entity.HasIndex(x => new { x.TrainingId, x.ContestId })
                .IsUnique();
        });

        modelBuilder.Entity<CodeforcesGym>(entity =>
        {
            entity.ToTable("codeforces_gyms");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(200);

            // Stored as the enum name (e.g. "Standings") rather than an int.
            entity.Property(x => x.FetchMethod)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(x => x.GymContestId)
                .IsUnique();
        });

        modelBuilder.Entity<UserProblemStatus>(entity =>
        {
            entity.ToTable("user_problem_statuses");

            entity.HasKey(x => x.Id);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Problem)
                .WithMany()
                .HasForeignKey(x => x.ProblemId)
                .OnDelete(DeleteBehavior.Cascade);

            // One status row per (user, problem).
            entity.HasIndex(x => new { x.UserId, x.ProblemId })
                .IsUnique();
        });
    }
}
