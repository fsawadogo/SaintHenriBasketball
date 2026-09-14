using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaintHenriBasketball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementPreferencesAndWaitlistOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OfferExpiresAt",
                table: "Waitlists",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CommunityUpdatesEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaymentRemindersEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SessionRemindersEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "WaitlistAlertsEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhotoConsentRecordedAt",
                table: "SessionRecaps",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OfferExpiresAt",
                table: "Waitlists");

            migrationBuilder.DropColumn(
                name: "CommunityUpdatesEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PaymentRemindersEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SessionRemindersEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WaitlistAlertsEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhotoConsentRecordedAt",
                table: "SessionRecaps");
        }
    }
}
