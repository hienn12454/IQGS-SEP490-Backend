using DomainLayer.Constants;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<HRProfile> HRProfiles { get; set; }
    public DbSet<CandidateProfile> CandidateProfiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Role (lookup table) ─────────────────────────────────────
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).ValueGeneratedNever();   // seed cố định
            entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(r => r.Name).IsUnique();

            // Seed 3 roles
            entity.HasData(
                new Role { Id = UserRole.AdminId, Name = UserRole.Admin },
                new Role { Id = UserRole.HRId, Name = UserRole.HR },
                new Role { Id = UserRole.CandidateId, Name = UserRole.Candidate }
            );
        });

        // ── User ────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(255);
            entity.Property(u => u.PasswordHash);                            // nullable — OAuth users
            entity.Property(u => u.PhoneNumber).HasMaxLength(20);
            entity.Property(u => u.AvatarUrl).HasMaxLength(500);

            // Role FK
            entity.Property(u => u.RoleId).IsRequired();
            entity.HasOne(u => u.Role)
                  .WithMany(r => r.Users)
                  .HasForeignKey(u => u.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Auth
            entity.Property(u => u.IsEmailVerified).IsRequired().HasDefaultValue(false);
            entity.Property(u => u.FailedLoginAttempts).IsRequired().HasDefaultValue(0);
            entity.Property(u => u.Provider).IsRequired().HasMaxLength(20).HasDefaultValue("local");
            entity.Property(u => u.GoogleId).HasMaxLength(255);
            entity.Property(u => u.RefreshToken).HasMaxLength(512);
            entity.HasIndex(u => u.RefreshToken);

            // Password reset & email verification — lưu SHA-256 hash
            entity.Property(u => u.PasswordResetToken).HasMaxLength(512);
            entity.Property(u => u.EmailVerificationToken).HasMaxLength(512);

            // Profile
            entity.Property(u => u.IsProfileComplete).IsRequired().HasDefaultValue(false);
        });

        // ── Company ─────────────────────────────────────────────────
        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(255);
            entity.Property(c => c.LogoUrl).HasMaxLength(500);
            entity.Property(c => c.WebsiteUrl).HasMaxLength(500);
            entity.HasIndex(c => c.Name);
        });

        // ── HRProfile ───────────────────────────────────────────────
        modelBuilder.Entity<HRProfile>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.JobTitle).HasMaxLength(150);
            entity.Property(p => p.PhoneNumber).HasMaxLength(20);
            entity.Property(p => p.LinkedInUrl).HasMaxLength(500);
            entity.Property(p => p.IsCompanyVerified).IsRequired().HasDefaultValue(false);

            entity.HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<HRProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(p => p.UserId).IsUnique();

            entity.HasOne(p => p.Company)
                .WithMany(c => c.HRProfiles)
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CandidateProfile ────────────────────────────────────────
        modelBuilder.Entity<CandidateProfile>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.TargetRole).HasMaxLength(150);
            entity.Property(p => p.SeniorityLevel).HasMaxLength(50);
            entity.Property(p => p.TechStack).HasColumnType("text[]");
            entity.Property(p => p.PhoneNumber).HasMaxLength(20);
            entity.Property(p => p.LinkedInUrl).HasMaxLength(500);
            entity.Property(p => p.GithubUrl).HasMaxLength(500);

            entity.HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<CandidateProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(p => p.UserId).IsUnique();
        });
    }
}
