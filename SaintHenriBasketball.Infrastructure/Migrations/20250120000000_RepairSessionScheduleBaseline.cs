using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SaintHenriBasketball.Infrastructure.Data.Context;

namespace SaintHenriBasketball.Infrastructure.Migrations;

// The historical chain altered Location before creating it, and omitted session
// times entirely. Existing installations may already have these columns.
[DbContext(typeof(ApplicationDbContext))]
[Migration("20250120000000_RepairSessionScheduleBaseline")]
public class RepairSessionScheduleBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF COL_LENGTH('Sessions', 'Location') IS NULL ALTER TABLE Sessions ADD Location nvarchar(max) NULL;");
        migrationBuilder.Sql("IF COL_LENGTH('Sessions', 'StartTime') IS NULL ALTER TABLE Sessions ADD StartTime nvarchar(max) NOT NULL CONSTRAINT DF_Sessions_StartTime_Baseline DEFAULT '10:00';");
        migrationBuilder.Sql("IF COL_LENGTH('Sessions', 'EndTime') IS NULL ALTER TABLE Sessions ADD EndTime nvarchar(max) NOT NULL CONSTRAINT DF_Sessions_EndTime_Baseline DEFAULT '12:00';");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve columns that might have existed before this repair.
    }
}
