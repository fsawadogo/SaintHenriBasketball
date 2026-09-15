using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SaintHenriBasketball.Domain.Entities;
using SaintHenriBasketball.Domain.Enums;

namespace SaintHenriBasketball.Infrastructure.Data.Configurations.CourtAttendance;

/// Court attendance outcome column. Merges with the inline SessionAttendance setup in ApplicationDbContext.
public class SessionAttendanceOutcomeConfiguration : IEntityTypeConfiguration<SessionAttendance>
{
    public void Configure(EntityTypeBuilder<SessionAttendance> builder)
    {
        builder.Property(a => a.Outcome)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(AttendanceOutcome.Unmarked)
            // Unmarked is also the CLR default, so EF always sends the value; the database default covers existing rows.
            .HasSentinel((AttendanceOutcome)(-1));
    }
}
