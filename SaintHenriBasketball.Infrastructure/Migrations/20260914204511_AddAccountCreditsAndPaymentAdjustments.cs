using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaintHenriBasketball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountCreditsAndPaymentAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditApplied",
                table: "Payments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Payments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalAmount",
                table: "Payments",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PromoCodeId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ReferralRedemptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountCredits_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountCredits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PromoCodeId",
                table: "Payments",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCredits_PaymentId_Kind",
                table: "AccountCredits",
                columns: new[] { "PaymentId", "Kind" },
                unique: true,
                filter: "[PaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCredits_ReferralRedemptionId",
                table: "AccountCredits",
                column: "ReferralRedemptionId",
                unique: true,
                filter: "[ReferralRedemptionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountCredits_UserId",
                table: "AccountCredits",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PromoCodes_PromoCodeId",
                table: "Payments",
                column: "PromoCodeId",
                principalTable: "PromoCodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PromoCodes_PromoCodeId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "AccountCredits");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PromoCodeId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CreditApplied",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "OriginalAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PromoCodeId",
                table: "Payments");
        }
    }
}
