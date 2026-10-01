using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Coaching.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachingCmsAndSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_transfer_requests",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PayerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_transfer_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cms_entries",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Group = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SeoTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SeoDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    ScheduledPublishAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cms_navigation_items",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Menu = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Icon = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    OpenInNewTab = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_navigation_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cms_revisions",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_revisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_records",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankTransferRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_plans",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    IsContactOnly = table.Column<bool>(type: "boolean", nullable: false),
                    BillingPeriod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    IncludedStudentSeats = table.Column<int>(type: "integer", nullable: true),
                    FeaturesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_plans", x => x.Id);
                    table.CheckConstraint("ck_coaching_subscription_plans_price", "\"Price\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "subscription_seats",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_seats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_settings",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequireActiveSubscription = table.Column<bool>(type: "boolean", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    AccountHolder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BankName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    PaymentInstructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BankTransferEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "coaching",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BankTransferRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.Id);
                    table.CheckConstraint("ck_coaching_subscription_dates", "\"EndDate\" >= \"StartDate\"");
                    table.CheckConstraint("ck_coaching_subscription_owner", "((\"UserId\" IS NOT NULL)::int + (\"InstitutionId\" IS NOT NULL)::int) = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_transfer_requests_Status_CreatedAt",
                schema: "coaching",
                table: "bank_transfer_requests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_transfer_requests_UserId_Status",
                schema: "coaching",
                table: "bank_transfer_requests",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_cms_entries_Kind_Group_SortOrder",
                schema: "coaching",
                table: "cms_entries",
                columns: new[] { "Kind", "Group", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_cms_entries_Kind_IsPublished_ScheduledPublishAt",
                schema: "coaching",
                table: "cms_entries",
                columns: new[] { "Kind", "IsPublished", "ScheduledPublishAt" });

            migrationBuilder.CreateIndex(
                name: "IX_cms_entries_Kind_Slug",
                schema: "coaching",
                table: "cms_entries",
                columns: new[] { "Kind", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cms_navigation_items_Menu_SortOrder",
                schema: "coaching",
                table: "cms_navigation_items",
                columns: new[] { "Menu", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_cms_revisions_EntryId_Version",
                schema: "coaching",
                table: "cms_revisions",
                columns: new[] { "EntryId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_records_BankTransferRequestId",
                schema: "coaching",
                table: "payment_records",
                column: "BankTransferRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_records_CreatedAt_Status",
                schema: "coaching",
                table: "payment_records",
                columns: new[] { "CreatedAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_IsPublic_IsActive_SortOrder",
                schema: "coaching",
                table: "subscription_plans",
                columns: new[] { "IsPublic", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_plans_Slug",
                schema: "coaching",
                table: "subscription_plans",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_seats_StudentId_IsSuspended",
                schema: "coaching",
                table: "subscription_seats",
                columns: new[] { "StudentId", "IsSuspended" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_seats_SubscriptionId_StudentId",
                schema: "coaching",
                table: "subscription_seats",
                columns: new[] { "SubscriptionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_BankTransferRequestId",
                schema: "coaching",
                table: "subscriptions",
                column: "BankTransferRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_InstitutionId_Status_EndDate",
                schema: "coaching",
                table: "subscriptions",
                columns: new[] { "InstitutionId", "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_UserId_Status_EndDate",
                schema: "coaching",
                table: "subscriptions",
                columns: new[] { "UserId", "Status", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_transfer_requests",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "cms_entries",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "cms_navigation_items",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "cms_revisions",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "payment_records",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "subscription_plans",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "subscription_seats",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "subscription_settings",
                schema: "coaching");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "coaching");
        }
    }
}
