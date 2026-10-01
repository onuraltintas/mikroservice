using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingInstitutionPaymentReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                schema: "coaching",
                table: "subscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_InstitutionId_PaymentReference",
                schema: "coaching",
                table: "subscriptions",
                columns: new[] { "InstitutionId", "PaymentReference" },
                unique: true,
                filter: "\"InstitutionId\" IS NOT NULL AND \"PaymentReference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_subscriptions_InstitutionId_PaymentReference",
                schema: "coaching",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                schema: "coaching",
                table: "subscriptions");
        }
    }
}
