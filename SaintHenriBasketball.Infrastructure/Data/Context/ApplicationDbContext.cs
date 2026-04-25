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
    public DbSet<Waitlist> Waitlists { get; set; }
    public DbSet<FeatureFlag> FeatureFlags { get; set; }
    public DbSet<SessionFeedback> SessionFeedbacks { get; set; }
    public DbSet<SessionRecap> SessionRecaps { get; set; }
    public DbSet<ReferralCode> ReferralCodes { get; set; }
    public DbSet<ReferralRedemption> ReferralRedemptions { get; set; }
    public DbSet<PromoCode> PromoCodes { get; set; }
    public DbSet<WaiverTemplate> WaiverTemplates { get; set; }
    public DbSet<WaiverAcceptance> WaiverAcceptances { get; set; }
    public DbSet<Notification> Notifications { get; set; }

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

            entity.Property(e => e.CalendarFeedToken)
                .HasMaxLength(100)
                .IsRequired(false);

            entity.Property(e => e.TwoFactorEnabled)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.TwoFactorSecret)
                .HasMaxLength(200)
                .IsRequired(false);

            entity.Property(e => e.EmergencyContactName).HasMaxLength(200);
            entity.Property(e => e.EmergencyContactPhone).HasMaxLength(40);
            entity.Property(e => e.MedicalAlerts).HasMaxLength(2000);
            entity.Property(e => e.PhoneNumber).HasMaxLength(40);
            entity.Property(e => e.SmsOptIn).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.EmailNotificationsEnabled).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.InAppNotificationsEnabled).IsRequired().HasDefaultValue(true);

            // Unique indexes
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.CalendarFeedToken).IsUnique().HasFilter("[CalendarFeedToken] IS NOT NULL");
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

                entity.HasOne(p => p.Session)
                    .WithMany()
                    .HasForeignKey(p => p.SessionId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Index serves the auto-billing idempotency query
                // (UserId + SessionId + Status filter to skip Refunded duplicates).
                entity.HasIndex(p => new { p.SessionId, p.UserId, p.Status });
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

        modelBuilder.Entity<Waitlist>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Position).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(w => w.Session)
                .WithMany()
                .HasForeignKey(w => w.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SessionId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<FeatureFlag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DescriptionFr).HasMaxLength(500);
            entity.Property(e => e.Enabled).IsRequired();
            entity.Property(e => e.IsPublic).IsRequired();
            entity.Property(e => e.CreatedOn).IsRequired();
            entity.HasIndex(e => e.Key).IsUnique();
        });

        modelBuilder.Entity<SessionFeedback>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Rating).IsRequired();
            entity.Property(e => e.Comment).HasMaxLength(1000);
            entity.Property(e => e.CreatedOn).IsRequired();
            entity.HasIndex(e => new { e.SessionId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.SessionId);
        });

        modelBuilder.Entity<SessionRecap>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PhotoUrl).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Caption).HasMaxLength(500);
            entity.Property(e => e.CreatedOn).IsRequired();
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CreatedOn);
        });

        modelBuilder.Entity<ReferralCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(16);
            entity.Property(e => e.CreatedOn).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.OwnerUserId).IsUnique();
        });

        modelBuilder.Entity<ReferralRedemption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RewardStatus).IsRequired();
            entity.Property(e => e.RedeemedOn).IsRequired();
            entity.HasIndex(e => e.RefereeUserId).IsUnique();
            entity.HasIndex(e => e.ReferrerUserId);
        });

        modelBuilder.Entity<PromoCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(32);
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.DiscountType).IsRequired();
            entity.Property(e => e.AppliesTo).IsRequired();
            entity.Property(e => e.ValidFrom).IsRequired();
            entity.Property(e => e.ValidUntil).IsRequired();
            entity.Property(e => e.CreatedOn).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<WaiverTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Version).IsRequired();
            entity.Property(e => e.BodyEn).IsRequired();
            entity.Property(e => e.BodyFr).IsRequired();
            entity.Property(e => e.EffectiveDate).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedOn).IsRequired();
            entity.HasIndex(e => e.Version).IsUnique();
        });

        modelBuilder.Entity<WaiverAcceptance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WaiverVersion).IsRequired();
            entity.Property(e => e.AcceptedAt).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(100);
            entity.HasIndex(e => new { e.UserId, e.WaiverVersion }).IsUnique();
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Body).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Url).HasMaxLength(2000);
            entity.Property(e => e.CreatedOn).IsRequired();
            // Drives the bell's "unread count" + "recent" queries.
            entity.HasIndex(e => new { e.UserId, e.ReadAt, e.CreatedOn });
        });
    }
}