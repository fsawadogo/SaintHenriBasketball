using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Infrastructure.Data.Configurations.VolunteerRoles;

/// Volunteer staff role column (Users.StaffRole, int, not null, default 0 = None).
/// Merges with the inline ApplicationUser setup in ApplicationDbContext.
public class ApplicationUserStaffRoleConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.StaffRole)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(StaffRole.None)
            // None is also the CLR default, so EF always sends the value; the database default covers existing rows.
            .HasSentinel((StaffRole)(-1));
    }
}
