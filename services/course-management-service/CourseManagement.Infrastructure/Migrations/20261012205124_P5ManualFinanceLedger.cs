using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P5ManualFinanceLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeeAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Model = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    BillingUnit = table.Column<int>(type: "integer", nullable: true),
                    PackageSessions = table.Column<int>(type: "integer", nullable: true),
                    PackageExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeAgreements_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeAgreements_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeAgreements_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReversalOfPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Payments_ReversalOfPaymentId",
                        column: x => x.ReversalOfPaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Installments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeAgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversalOfInstallmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Installments_FeeAgreements_FeeAgreementId",
                        column: x => x.FeeAgreementId,
                        principalTable: "FeeAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Installments_Installments_ReversalOfInstallmentId",
                        column: x => x.ReversalOfInstallmentId,
                        principalTable: "Installments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Installments_LessonSessions_SourceLessonId",
                        column: x => x.SourceLessonId,
                        principalTable: "LessonSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Installments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Installments_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PackageMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeeAgreementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageMovements_FeeAgreements_FeeAgreementId",
                        column: x => x.FeeAgreementId,
                        principalTable: "FeeAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageMovements_LessonSessions_LessonSessionId",
                        column: x => x.LessonSessionId,
                        principalTable: "LessonSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageMovements_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageMovements_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Installments_InstallmentId",
                        column: x => x.InstallmentId,
                        principalTable: "Installments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeAgreements_CourseId",
                table: "FeeAgreements",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAgreements_StudentId",
                table: "FeeAgreements",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAgreements_WorkspaceId_StudentId_CourseId",
                table: "FeeAgreements",
                columns: new[] { "WorkspaceId", "StudentId", "CourseId" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_FeeAgreements_WorkspaceId_StudentId_Model_IsActive",
                table: "FeeAgreements",
                columns: new[] { "WorkspaceId", "StudentId", "Model", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Installments_FeeAgreementId_SourceLessonId",
                table: "Installments",
                columns: new[] { "FeeAgreementId", "SourceLessonId" },
                unique: true,
                filter: "\"SourceLessonId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Installments_ReversalOfInstallmentId",
                table: "Installments",
                column: "ReversalOfInstallmentId",
                unique: true,
                filter: "\"ReversalOfInstallmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Installments_SourceLessonId",
                table: "Installments",
                column: "SourceLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_Installments_StudentId",
                table: "Installments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Installments_WorkspaceId_StudentId_DueOn",
                table: "Installments",
                columns: new[] { "WorkspaceId", "StudentId", "DueOn" });

            migrationBuilder.CreateIndex(
                name: "IX_PackageMovements_FeeAgreementId_LessonSessionId",
                table: "PackageMovements",
                columns: new[] { "FeeAgreementId", "LessonSessionId" },
                unique: true,
                filter: "\"LessonSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PackageMovements_LessonSessionId",
                table: "PackageMovements",
                column: "LessonSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageMovements_StudentId",
                table: "PackageMovements",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageMovements_WorkspaceId_StudentId_OccurredAt",
                table: "PackageMovements",
                columns: new[] { "WorkspaceId", "StudentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_InstallmentId",
                table: "PaymentAllocations",
                column: "InstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PaymentId_InstallmentId",
                table: "PaymentAllocations",
                columns: new[] { "PaymentId", "InstallmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_WorkspaceId_InstallmentId",
                table: "PaymentAllocations",
                columns: new[] { "WorkspaceId", "InstallmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReversalOfPaymentId",
                table: "Payments",
                column: "ReversalOfPaymentId",
                unique: true,
                filter: "\"ReversalOfPaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StudentId",
                table: "Payments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_WorkspaceId_StudentId_ReceivedAt",
                table: "Payments",
                columns: new[] { "WorkspaceId", "StudentId", "ReceivedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageMovements");

            migrationBuilder.DropTable(
                name: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "Installments");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "FeeAgreements");
        }
    }
}
