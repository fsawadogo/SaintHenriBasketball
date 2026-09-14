using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaintHenriBasketball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LinkPaymentsToSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SeasonId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SeasonId_UserId_Status",
                table: "Payments",
                columns: new[] { "SeasonId", "UserId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Seasons_SeasonId",
                table: "Payments",
                column: "SeasonId",
                principalTable: "Seasons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Seasons_SeasonId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_SeasonId_UserId_Status",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "SeasonId",
                table: "Payments");
        }
    }
}
