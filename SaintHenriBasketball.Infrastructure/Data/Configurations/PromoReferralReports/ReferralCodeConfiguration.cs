using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaintHenriBasketball.Domain.Entities;

namespace SaintHenriBasketball.Infrastructure.Data.Configurations.PromoReferralReports;

/// Admin controls on referral codes. Merges with the inline ReferralCode setup in ApplicationDbContext.
public class ReferralCodeConfiguration : IEntityTypeConfiguration<ReferralCode>
{
    public void Configure(EntityTypeBuilder<ReferralCode> builder)
    {
        // Existing codes become active. The entity's nullable backing field (_isActive) lets EF tell an
        // explicit false from "not set", so an inactive code is never saved as active by the default.
        builder.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);

        // Null means unlimited.
        builder.Property(c => c.MaxUses).IsRequired(false);
    }
}
