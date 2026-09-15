using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Infrastructure.Data.Configurations.OutstandingBalances;

public class ReminderLogConfiguration : IEntityTypeConfiguration<ReminderLog>
{
    public void Configure(EntityTypeBuilder<ReminderLog> entity)
    {
        entity.ToTable("ReminderLogs");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Kind).IsRequired().HasMaxLength(32);
        entity.Property(e => e.Channel).IsRequired().HasMaxLength(16).HasDefaultValue(ReminderChannels.Email);
        entity.Property(e => e.Status).IsRequired().HasMaxLength(16);
        entity.Property(e => e.Reason).HasMaxLength(ReminderLog.MaxReasonLength);
        entity.Property(e => e.SentAt).IsRequired();

        // History of a deleted account has no one to show it for. Payments and seasons are not linked by key:
        // the history outlives a deleted session payment or season.
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.UserId, e.SentAt });
        entity.HasIndex(e => e.SentAt);
    }
}
