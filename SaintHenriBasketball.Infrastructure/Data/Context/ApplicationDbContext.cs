using SaintHenriBasketball.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SaintHenriBasketball.Infrastructure.Data.Context;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationUser> Users { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<SessionRegistration> SessionRegistrations { get; set; }
    public DbSet<SeasonSubscription> SeasonSubscriptions { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<SessionAttendance> SessionAttendances { get; set; }
    public DbSet<Season> Seasons { get; set; }
    public DbSet<SeasonRegistration> SeasonRegistrations { get; set; }
    public DbSet<EmailLog> EmailLogs { get; set; }
    public DbSet<SavedEmailTemplate> SavedEmailTemplates { get; set; }
    public DbSet<SessionTemplate> SessionTemplates { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Required fields
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.PasswordHash)
                .IsRequired();

            entity.Property(e => e.FirstName)
                .IsRequired();

            entity.Property(e => e.LastName)
                .IsRequired();

            entity.Property(e => e.CreatedOn)
                .IsRequired();

            entity.Property(e => e.PaymentPlan)
                .IsRequired();

            // Nullable fields for email confirmation and password reset
            entity.Property(e => e.EmailConfirmationToken)
                .HasMaxLength(450)
                .IsRequired(false);

            entity.Property(e => e.EmailConfirmed)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.PasswordResetToken)
                .HasMaxLength(450)
                .IsRequired(false);

            entity.Property(e => e.PasswordResetTokenExpiry)
                .IsRequired(false);

            // Unique indexes
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionDate).IsRequired();
            entity.Property(e => e.MaxCapacity).IsRequired();
            entity.Property(e => e.DropInPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.RegisteredPlayersCount).IsRequired();
            entity.Property(e => e.CreatedOn).IsRequired();
        });

        modelBuilder.Entity<SessionRegistration>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PaymentPlan).IsRequired();
            entity.Property(e => e.RegistrationDate).IsRequired();

            entity.HasOne(sr => sr.User)
                .WithMany(u => u.SessionRegistrations)
                .HasForeignKey(sr => sr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sr => sr.Session)
                .WithMany(s => s.Registrations)
                .HasForeignKey(sr => sr.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SeasonSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.StartDate)
                .IsRequired();

            entity.Property(e => e.EndDate)
                .IsRequired();

            entity.Property(e => e.CreatedOn)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .IsRequired();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();

                entity.Property(e => e.Plan)
                    .IsRequired();

                entity.Property(e => e.Status)
                    .IsRequired();

                entity.Property(e => e.PaymentDate)
                    .IsRequired();

                entity.HasOne(p => p.User)
                    .WithMany()
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

        modelBuilder.Entity<SessionAttendance>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CreatedOn)
                .IsRequired();
            
            entity.Property(e => e.Notes)
                .HasMaxLength(500);

            entity.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SessionId, e.UserId })
                .IsUnique();
        });

        modelBuilder.Entity<Season>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.Property(e => e.StartDate).IsRequired();
            entity.Property(e => e.EndDate).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<SeasonRegistration>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Season)
                .WithMany(s => s.Registrations)
                .HasForeignKey(e => e.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.RegisteredOn)
                .IsRequired();
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Recipient).IsRequired().HasMaxLength(450);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(500);
            entity.Property(e => e.EmailType).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.SentAt).IsRequired();
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.Property(e => e.RecipientName).HasMaxLength(200);
            entity.HasIndex(e => e.SentAt);
            entity.HasIndex(e => e.Recipient);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<SavedEmailTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SubjectEn).HasMaxLength(500);
            entity.Property(e => e.SubjectFr).HasMaxLength(500);
        });

        modelBuilder.Entity<SessionTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DropInPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Location).HasMaxLength(500);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.EntityType);
        });
    }
}