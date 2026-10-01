using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingBankTransferRequestReferenceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM coaching.bank_transfer_requests
                        GROUP BY "UserId", "PaymentReference"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce unique coaching EFT references: duplicate user/reference rows already exist.';
                    END IF;
                END
                $migration$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_bank_transfer_requests_UserId_PaymentReference",
                schema: "coaching",
                table: "bank_transfer_requests",
                columns: new[] { "UserId", "PaymentReference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bank_transfer_requests_UserId_PaymentReference",
                schema: "coaching",
                table: "bank_transfer_requests");
        }
    }
}
