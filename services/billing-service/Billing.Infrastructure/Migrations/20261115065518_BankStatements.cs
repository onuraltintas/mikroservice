using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BankStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_statement_imports",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    AddedCount = table.Column<int>(type: "integer", nullable: false),
                    DuplicateCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedCount = table.Column<int>(type: "integer", nullable: false),
                    AutoApprovedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statement_imports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bank_statement_imports_bank_accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "billing",
                        principalTable: "bank_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_transactions",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Sender = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PaymentCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bank_transactions_bank_accounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "billing",
                        principalTable: "bank_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_transactions_bank_statement_imports_ImportId",
                        column: x => x.ImportId,
                        principalSchema: "billing",
                        principalTable: "bank_statement_imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_transactions_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "billing",
                        principalTable: "orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_bank_transactions_payments_PaymentId",
                        column: x => x.PaymentId,
                        principalSchema: "billing",
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_statement_imports_BankAccountId",
                schema: "billing",
                table: "bank_statement_imports",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_statement_imports_UploadedAt",
                schema: "billing",
                table: "bank_statement_imports",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transactions_BankAccountId",
                schema: "billing",
                table: "bank_transactions",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transactions_Fingerprint",
                schema: "billing",
                table: "bank_transactions",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_transactions_ImportId",
                schema: "billing",
                table: "bank_transactions",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transactions_OrderId",
                schema: "billing",
                table: "bank_transactions",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transactions_PaymentId",
                schema: "billing",
                table: "bank_transactions",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_transactions_Status_BookedAt",
                schema: "billing",
                table: "bank_transactions",
                columns: new[] { "Status", "BookedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_transactions",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "bank_statement_imports",
                schema: "billing");
        }
    }
}
