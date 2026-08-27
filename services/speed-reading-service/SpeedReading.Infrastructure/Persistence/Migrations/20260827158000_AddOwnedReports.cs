using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827158000_AddOwnedReports")]
public partial class AddOwnedReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "report_templates",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                type = table.Column<int>(type: "integer", nullable: false),
                category = table.Column<int>(type: "integer", nullable: false),
                configuration_json = table.Column<string>(type: "text", nullable: false),
                is_system_template = table.Column<bool>(type: "boolean", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_report_templates", x => x.id));

        migrationBuilder.CreateTable(
            name: "report_snapshots",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                report_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                generated_for_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                generated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                report_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                report_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                data_json = table.Column<string>(type: "text", nullable: false),
                pdf_file_url = table.Column<string>(type: "text", nullable: true),
                excel_file_url = table.Column<string>(type: "text", nullable: true),
                is_viewed = table.Column<bool>(type: "boolean", nullable: false),
                viewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_report_snapshots", x => x.id);
                table.ForeignKey(
                    name: "fk_report_snapshots_report_templates_report_template_id",
                    column: x => x.report_template_id,
                    principalSchema: "speed_reading",
                    principalTable: "report_templates",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "scheduled_reports",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                report_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                frequency = table.Column<int>(type: "integer", nullable: false),
                day_of_week = table.Column<int>(type: "integer", nullable: true),
                day_of_month = table.Column<int>(type: "integer", nullable: true),
                delivery_time = table.Column<TimeSpan>(type: "time without time zone", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                last_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                success_count = table.Column<int>(type: "integer", nullable: false),
                failure_count = table.Column<int>(type: "integer", nullable: false),
                send_email = table.Column<bool>(type: "boolean", nullable: false),
                save_to_dashboard = table.Column<bool>(type: "boolean", nullable: false),
                email_recipients = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_scheduled_reports", x => x.id);
                table.ForeignKey(
                    name: "fk_scheduled_reports_report_templates_report_template_id",
                    column: x => x.report_template_id,
                    principalSchema: "speed_reading",
                    principalTable: "report_templates",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_report_templates_type_is_active_is_deleted",
            schema: "speed_reading",
            table: "report_templates",
            columns: new[] { "type", "is_active", "is_deleted" });
        migrationBuilder.CreateIndex(
            name: "ix_report_snapshots_report_template_id",
            schema: "speed_reading",
            table: "report_snapshots",
            column: "report_template_id");
        migrationBuilder.CreateIndex(
            name: "ix_report_snapshots_generated_for_user_id_generated_at",
            schema: "speed_reading",
            table: "report_snapshots",
            columns: new[] { "generated_for_user_id", "generated_at" });
        migrationBuilder.CreateIndex(
            name: "ix_scheduled_reports_report_template_id",
            schema: "speed_reading",
            table: "scheduled_reports",
            column: "report_template_id");
        migrationBuilder.CreateIndex(
            name: "ix_scheduled_reports_user_id_is_active_next_run_at",
            schema: "speed_reading",
            table: "scheduled_reports",
            columns: new[] { "user_id", "is_active", "next_run_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "report_snapshots", schema: "speed_reading");
        migrationBuilder.DropTable(name: "scheduled_reports", schema: "speed_reading");
        migrationBuilder.DropTable(name: "report_templates", schema: "speed_reading");
    }
}
