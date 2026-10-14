using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionRequirementToggle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireActiveSubscription",
                schema: "speed_reading",
                table: "bank_transfer_payment_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireActiveSubscription",
                schema: "speed_reading",
                table: "bank_transfer_payment_settings");
        }
    }
}
