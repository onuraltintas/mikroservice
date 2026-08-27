using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827145000_AddOwnedLearningPaths")]
public partial class AddOwnedLearningPaths : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "learning_path_templates",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                TargetAgeGroupConfigurationId = table.Column<Guid>(type: "uuid", nullable: true),
                Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                TotalNodes = table.Column<int>(type: "integer", nullable: false),
                EstimatedDays = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_learning_path_templates", x => x.id);
                table.ForeignKey(
                    name: "fk_learning_path_templates_age_group_configurations_target_age_group_configuration_id",
                    column: x => x.TargetAgeGroupConfigurationId,
                    principalSchema: "speed_reading",
                    principalTable: "age_group_configurations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "learning_path_nodes",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                ParentNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                NodeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ContentId = table.Column<Guid>(type: "uuid", nullable: true),
                Order = table.Column<int>(type: "integer", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_learning_path_nodes", x => x.id);
                table.ForeignKey(
                    name: "fk_learning_path_nodes_learning_path_nodes_parent_node_id",
                    column: x => x.ParentNodeId,
                    principalSchema: "speed_reading",
                    principalTable: "learning_path_nodes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_learning_path_nodes_learning_path_templates_template_id",
                    column: x => x.TemplateId,
                    principalSchema: "speed_reading",
                    principalTable: "learning_path_templates",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "learning_path_node_contents",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                ExerciseId = table.Column<Guid>(type: "uuid", nullable: true),
                ReadingTextId = table.Column<Guid>(type: "uuid", nullable: true),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_learning_path_node_contents", x => x.id);
                table.ForeignKey(
                    name: "fk_learning_path_node_contents_exercises_exercise_id",
                    column: x => x.ExerciseId,
                    principalSchema: "speed_reading",
                    principalTable: "exercises",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_learning_path_node_contents_learning_path_nodes_node_id",
                    column: x => x.NodeId,
                    principalSchema: "speed_reading",
                    principalTable: "learning_path_nodes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_learning_path_node_contents_reading_texts_reading_text_id",
                    column: x => x.ReadingTextId,
                    principalSchema: "speed_reading",
                    principalTable: "reading_texts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "learning_path_prerequisites",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                PrerequisiteNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_learning_path_prerequisites", x => x.id);
                table.ForeignKey(
                    name: "fk_learning_path_prerequisites_learning_path_nodes_node_id",
                    column: x => x.NodeId,
                    principalSchema: "speed_reading",
                    principalTable: "learning_path_nodes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_learning_path_prerequisites_learning_path_nodes_prerequisite_node_id",
                    column: x => x.PrerequisiteNodeId,
                    principalSchema: "speed_reading",
                    principalTable: "learning_path_nodes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "student_learning_path_progress",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                CurrentNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                Progress = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_student_learning_path_progress", x => x.id);
                table.ForeignKey(
                    name: "fk_student_learning_path_progress_learning_path_templates_template_id",
                    column: x => x.TemplateId,
                    principalSchema: "speed_reading",
                    principalTable: "learning_path_templates",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "student_learning_node_progress",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_student_learning_node_progress", x => x.id);
                table.ForeignKey(
                    name: "fk_student_learning_node_progress_learning_path_nodes_node_id",
                    column: x => x.NodeId,
                    principalSchema: "speed_reading",
                    principalTable: "learning_path_nodes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "personalized_learning_path_items",
            schema: "speed_reading",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                PathIndex = table.Column<int>(type: "integer", nullable: false),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ContentId = table.Column<Guid>(type: "uuid", nullable: true),
                ContentTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                DifficultyLevel = table.Column<int>(type: "integer", nullable: false),
                EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AchievedScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                RecommendationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                IsUnlocked = table.Column<bool>(type: "boolean", nullable: false),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_personalized_learning_path_items", x => x.id);
                table.ForeignKey(
                    name: "fk_personalized_learning_path_items_learning_path_templates_template_id",
                    column: x => x.TemplateId,
                    principalSchema: "speed_reading",
                    principalTable: "learning_path_templates",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_learning_path_templates_name",
            schema: "speed_reading",
            table: "learning_path_templates",
            column: "Name");
        migrationBuilder.CreateIndex(
            name: "ix_learning_path_templates_is_active_is_deleted",
            schema: "speed_reading",
            table: "learning_path_templates",
            columns: new[] { "IsActive", "IsDeleted" });
        migrationBuilder.CreateIndex(
            name: "ix_learning_path_templates_target_age_group_configuration_id",
            schema: "speed_reading",
            table: "learning_path_templates",
            column: "TargetAgeGroupConfigurationId");
        migrationBuilder.CreateIndex(
            name: "ix_learning_path_nodes_template_id_is_deleted_order",
            schema: "speed_reading",
            table: "learning_path_nodes",
            columns: new[] { "TemplateId", "IsDeleted", "Order" });
        migrationBuilder.CreateIndex(
            name: "ix_learning_path_nodes_parent_node_id",
            schema: "speed_reading",
            table: "learning_path_nodes",
            column: "ParentNodeId");
        migrationBuilder.CreateIndex(
            name: "ix_learning_path_nodes_parent_node_id_is_deleted",
            schema: "speed_reading",
            table: "learning_path_nodes",
            columns: new[] { "ParentNodeId", "IsDeleted" });
        migrationBuilder.CreateIndex(
            name: "ix_learning_path_node_contents_node_id_is_deleted",
            schema: "speed_reading",
            table: "learning_path_node_contents",
            columns: new[] { "NodeId", "IsDeleted" });
        migrationBuilder.CreateIndex(
            name: "ix_learning_path_node_contents_exercise_id",
            schema: "speed_reading",
            table: "learning_path_node_contents",
            column: "ExerciseId");
        migrationBuilder.CreateIndex(
            name: "ix_learning_path_node_contents_reading_text_id",
            schema: "speed_reading",
            table: "learning_path_node_contents",
            column: "ReadingTextId");
        migrationBuilder.CreateIndex(
            name: "ix_learning_path_prerequisites_node_id_prerequisite_node_id_is_deleted",
            schema: "speed_reading",
            table: "learning_path_prerequisites",
            columns: new[] { "NodeId", "PrerequisiteNodeId", "IsDeleted" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_learning_path_prerequisites_prerequisite_node_id",
            schema: "speed_reading",
            table: "learning_path_prerequisites",
            column: "PrerequisiteNodeId");
        migrationBuilder.CreateIndex(
            name: "ix_student_learning_path_progress_student_id_template_id_is_deleted",
            schema: "speed_reading",
            table: "student_learning_path_progress",
            columns: new[] { "StudentId", "TemplateId", "IsDeleted" });
        migrationBuilder.CreateIndex(
            name: "ix_student_learning_path_progress_student_id_created_at",
            schema: "speed_reading",
            table: "student_learning_path_progress",
            columns: new[] { "StudentId", "created_at" });
        migrationBuilder.CreateIndex(
            name: "ix_student_learning_node_progress_student_id_node_id_is_deleted",
            schema: "speed_reading",
            table: "student_learning_node_progress",
            columns: new[] { "StudentId", "NodeId", "IsDeleted" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_student_learning_node_progress_student_id_status",
            schema: "speed_reading",
            table: "student_learning_node_progress",
            columns: new[] { "StudentId", "Status" });
        migrationBuilder.CreateIndex(
            name: "ix_personalized_learning_path_items_student_id_path_index_is_deleted",
            schema: "speed_reading",
            table: "personalized_learning_path_items",
            columns: new[] { "StudentId", "PathIndex", "IsDeleted" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "ix_personalized_learning_path_items_student_id_is_completed_is_unlocked",
            schema: "speed_reading",
            table: "personalized_learning_path_items",
            columns: new[] { "StudentId", "IsCompleted", "IsUnlocked" });
        migrationBuilder.CreateIndex(
            name: "ix_personalized_learning_path_items_template_id",
            schema: "speed_reading",
            table: "personalized_learning_path_items",
            column: "TemplateId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "personalized_learning_path_items", schema: "speed_reading");
        migrationBuilder.DropTable(name: "student_learning_node_progress", schema: "speed_reading");
        migrationBuilder.DropTable(name: "student_learning_path_progress", schema: "speed_reading");
        migrationBuilder.DropTable(name: "learning_path_prerequisites", schema: "speed_reading");
        migrationBuilder.DropTable(name: "learning_path_node_contents", schema: "speed_reading");
        migrationBuilder.DropTable(name: "learning_path_nodes", schema: "speed_reading");
        migrationBuilder.DropTable(name: "learning_path_templates", schema: "speed_reading");
    }
}
