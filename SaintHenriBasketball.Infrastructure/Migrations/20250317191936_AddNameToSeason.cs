using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaintHenriBasketball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNameToSeason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Seasons",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Seasons");
        }
    }
}
