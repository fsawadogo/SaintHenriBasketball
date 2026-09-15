using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaintHenriBasketball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdminAuditAccountSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountCredits_Users_UserId",
                table: "AccountCredits");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments");

            migrationBuilder.AddColumn<DateTime>(
                name: "AnonymizedOn",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedOn",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeactivated",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountCredits_Users_UserId",
                table: "AccountCredits",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountCredits_Users_UserId",
                table: "AccountCredits");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "AnonymizedOn",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeactivatedOn",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDeactivated",
                table: "Users");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountCredits_Users_UserId",
                table: "AccountCredits",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_UserId",
                table: "Payments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
