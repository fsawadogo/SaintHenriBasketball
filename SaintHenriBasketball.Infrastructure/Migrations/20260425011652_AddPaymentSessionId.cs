using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaintHenriBasketball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SessionId_UserId_Status",
                table: "Payments",
                columns: new[] { "SessionId", "UserId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Sessions_SessionId",
                table: "Payments",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Sessions_SessionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_SessionId_UserId_Status",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Payments");
        }
    }
}
