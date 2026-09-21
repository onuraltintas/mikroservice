using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations;

public partial class AddCoachingAgreementEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "coaching_agreement_documents",
            schema: "coaching",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                institution_id = table.Column<Guid>(type: "uuid", nullable: true),
                document_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                document_reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                content_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                published_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                effective_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                superseded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                row_version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_coaching_agreement_documents", item => item.id));

        migrationBuilder.CreateTable(
            name: "coaching_agreement_acknowledgements",
            schema: "coaching",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                agreement_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                subject_student_id = table.Column<Guid>(type: "uuid", nullable: false),
                acknowledged_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                party_role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                acknowledged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                withdrawn_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                withdrawn_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_coaching_agreement_acknowledgements", item => item.id);
                table.ForeignKey(
                    name: "FK_coaching_agreement_acknowledgements_coaching_agreement_documents_agreement_document_id",
                    column: item => item.agreement_document_id,
                    principalSchema: "coaching",
                    principalTable: "coaching_agreement_documents",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("ix_coaching_agreement_acknowledgements_student_timeline", "coaching_agreement_acknowledgements", new[] { "subject_student_id", "acknowledged_at" }, schema: "coaching");
        migrationBuilder.CreateIndex("ux_coaching_agreement_acknowledgements_active_evidence", "coaching_agreement_acknowledgements", new[] { "agreement_document_id", "subject_student_id", "acknowledged_by_user_id", "party_role" }, schema: "coaching", unique: true, filter: "withdrawn_at IS NULL");
        migrationBuilder.CreateIndex("ix_coaching_agreement_documents_current_lookup", "coaching_agreement_documents", new[] { "institution_id", "locale", "effective_at" }, schema: "coaching");
        migrationBuilder.CreateIndex("ux_coaching_agreement_documents_global_version_locale", "coaching_agreement_documents", new[] { "document_version", "locale" }, schema: "coaching", unique: true, filter: "institution_id IS NULL");
        migrationBuilder.CreateIndex("ux_coaching_agreement_documents_tenant_version_locale", "coaching_agreement_documents", new[] { "institution_id", "document_version", "locale" }, schema: "coaching", unique: true, filter: "institution_id IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("coaching_agreement_acknowledgements", schema: "coaching");
        migrationBuilder.DropTable("coaching_agreement_documents", schema: "coaching");
    }
}
