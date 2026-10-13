using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingFeeLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_reminder_kind", schema: "coaching", table: "reminder_receipts");
            migrationBuilder.AddCheckConstraint(name: "ck_reminder_kind", schema: "coaching", table: "reminder_receipts",
                sql: "\"Kind\" IN ('StudyDay', 'Session', 'FeeOverdue')");
            migrationBuilder.AddColumn<int>(
                name: "FinanceRecordCount", schema: "coaching", table: "CoachingErasureAssessments",
                type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.CreateTable(
                name: "fee_agreements",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoachId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(11,2)", precision: 11, scale: 2, nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAmount = table.Column<decimal>(type: "numeric(11,2)", precision: 11, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_agreements", x => x.Id);
                    table.CheckConstraint("ck_fee_amounts", "\"TotalAmount\" > 0 AND \"TotalAmount\" <= 999999999.99 AND \"PaidAmount\" BETWEEN 0 AND \"TotalAmount\"");
                    table.CheckConstraint("ck_fee_cancelled", "NOT \"IsCancelled\" OR (\"PaidAmount\" = 0 AND \"CancelledAt\" IS NOT NULL AND \"CancelledByUserId\" IS NOT NULL AND \"CancellationReason\" IS NOT NULL)");
                    table.CheckConstraint("ck_fee_owner", "(\"InstitutionId\" IS NOT NULL) <> (\"CoachId\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "fee_installments",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(11,2)", precision: 11, scale: 2, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_installments", x => x.Id);
                    table.CheckConstraint("ck_fee_installment", "\"Amount\" > 0 AND \"Amount\" <= 999999999.99 AND \"Sequence\" BETWEEN 1 AND 24");
                    table.ForeignKey(
                        name: "FK_fee_installments_fee_agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalSchema: "coaching",
                        principalTable: "fee_agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fee_payments",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReversalOfPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(11,2)", precision: 11, scale: 2, nullable: false),
                    ReceivedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Reference = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_payments", x => x.Id);
                    table.UniqueConstraint("AK_fee_payments_AgreementId_Id", x => new { x.AgreementId, x.Id });
                    table.CheckConstraint("ck_fee_payment_amount", "ABS(\"Amount\") BETWEEN 0.01 AND 999999999.99 AND ((\"ReversalOfPaymentId\" IS NULL AND \"Amount\" > 0) OR (\"ReversalOfPaymentId\" IS NOT NULL AND \"Amount\" < 0))");
                    table.CheckConstraint("ck_fee_payment_method", "\"Method\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_fee_payments_fee_agreements_AgreementId",
                        column: x => x.AgreementId,
                        principalSchema: "coaching",
                        principalTable: "fee_agreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fee_payments_fee_payments_AgreementId_ReversalOfPaymentId",
                        columns: x => new { x.AgreementId, x.ReversalOfPaymentId },
                        principalSchema: "coaching",
                        principalTable: "fee_payments",
                        principalColumns: new[] { "AgreementId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_fee_agreements_CoachId_StudentId",
                schema: "coaching",
                table: "fee_agreements",
                columns: new[] { "CoachId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_fee_agreements_InstitutionId_StudentId",
                schema: "coaching",
                table: "fee_agreements",
                columns: new[] { "InstitutionId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_fee_agreements_StudentId",
                schema: "coaching",
                table: "fee_agreements",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_fee_installments_AgreementId_Sequence",
                schema: "coaching",
                table: "fee_installments",
                columns: new[] { "AgreementId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fee_installments_DueOn",
                schema: "coaching",
                table: "fee_installments",
                column: "DueOn");

            migrationBuilder.CreateIndex(
                name: "IX_fee_payments_AgreementId_ReceivedOn",
                schema: "coaching",
                table: "fee_payments",
                columns: new[] { "AgreementId", "ReceivedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_fee_payments_AgreementId_ReversalOfPaymentId",
                schema: "coaching",
                table: "fee_payments",
                columns: new[] { "AgreementId", "ReversalOfPaymentId" });

            migrationBuilder.CreateIndex(
                name: "IX_fee_payments_ReversalOfPaymentId",
                schema: "coaching",
                table: "fee_payments",
                column: "ReversalOfPaymentId",
                unique: true,
                filter: "\"ReversalOfPaymentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM coaching.reminder_receipts WHERE \"Kind\" = 'FeeOverdue'");
            migrationBuilder.DropCheckConstraint(name: "ck_reminder_kind", schema: "coaching", table: "reminder_receipts");
            migrationBuilder.AddCheckConstraint(name: "ck_reminder_kind", schema: "coaching", table: "reminder_receipts",
                sql: "\"Kind\" IN ('StudyDay', 'Session')");
            migrationBuilder.DropColumn(name: "FinanceRecordCount", schema: "coaching", table: "CoachingErasureAssessments");
            migrationBuilder.DropTable(
                name: "fee_installments",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "fee_payments",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "fee_agreements",
                schema: "coaching");
        }
    }
}
