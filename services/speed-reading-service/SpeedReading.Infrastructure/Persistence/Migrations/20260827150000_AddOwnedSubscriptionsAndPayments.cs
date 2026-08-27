using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260827150000_AddOwnedSubscriptionsAndPayments")]
public partial class AddOwnedSubscriptionsAndPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "subscription_products", schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                included_product_slugs = table.Column<string>(type: "jsonb", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_subscription_products", x => x.Id));

        migrationBuilder.CreateTable(
            name: "subscription_plans", schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                BillingPeriod = table.Column<string>(type: "text", nullable: false),
                DurationDays = table.Column<int>(type: "integer", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                Features = table.Column<string>(type: "jsonb", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            }, constraints: table =>
            {
                table.PrimaryKey("pk_subscription_plans", x => x.Id);
                table.ForeignKey("fk_subscription_plans_subscription_products_product_id", x => x.ProductId,
                    principalSchema: "speed_reading", principalTable: "subscription_products", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "user_subscriptions", schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            }, constraints: table =>
            {
                table.PrimaryKey("pk_user_subscriptions", x => x.Id);
                table.ForeignKey("fk_user_subscriptions_subscription_plans_plan_id", x => x.PlanId,
                    principalSchema: "speed_reading", principalTable: "subscription_plans", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_user_subscriptions_subscription_products_product_id", x => x.ProductId,
                    principalSchema: "speed_reading", principalTable: "subscription_products", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "payments", schema: "speed_reading",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                UserEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ProviderToken = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ProviderPaymentId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ProviderResponse = table.Column<string>(type: "jsonb", nullable: true),
                ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("pk_payments", x => x.Id);
                table.ForeignKey("fk_payments_subscription_plans_plan_id", x => x.PlanId,
                    principalSchema: "speed_reading", principalTable: "subscription_plans", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("ix_subscription_products_slug", "subscription_products", "Slug", "speed_reading", true);
        migrationBuilder.CreateIndex("ix_subscription_plans_product_id", "subscription_plans", "ProductId", "speed_reading");
        migrationBuilder.CreateIndex("ix_subscription_plans_slug", "subscription_plans", "Slug", "speed_reading", true);
        migrationBuilder.CreateIndex("ix_user_subscriptions_user_id_status", "user_subscriptions", new[] { "UserId", "Status" }, "speed_reading");
        migrationBuilder.CreateIndex("ix_user_subscriptions_user_id_plan_id", "user_subscriptions", new[] { "UserId", "PlanId" }, "speed_reading");
        migrationBuilder.CreateIndex("ix_user_subscriptions_user_id_product_id", "user_subscriptions", new[] { "UserId", "ProductId" }, "speed_reading");
        migrationBuilder.CreateIndex("ix_payments_user_id", "payments", "UserId", "speed_reading");
        migrationBuilder.CreateIndex("ix_payments_status", "payments", "Status", "speed_reading");
        migrationBuilder.CreateIndex("ix_payments_plan_id", "payments", "PlanId", "speed_reading");
        migrationBuilder.CreateIndex("ix_payments_provider_token", "payments", "ProviderToken", "speed_reading", true, "\"ProviderToken\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "payments", schema: "speed_reading");
        migrationBuilder.DropTable(name: "user_subscriptions", schema: "speed_reading");
        migrationBuilder.DropTable(name: "subscription_plans", schema: "speed_reading");
        migrationBuilder.DropTable(name: "subscription_products", schema: "speed_reading");
    }
}
