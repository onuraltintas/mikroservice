using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SpeedReading.Infrastructure.Persistence.Migrations;

/// <summary>
/// Legal check Y1 (user decision 14.11.2026): Hızlı Okuma purchases get the pre-information form, the distance-sales
/// contract and the withdrawal (cayma) request. The seller block lives with the bank-transfer settings; what a buyer
/// accepted and the withdrawal live with the payment request. Every column is nullable, so existing rows are untouched.
/// </summary>
[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20261114190000_SalesDocumentsAndWithdrawal")]
public sealed class SalesDocumentsAndWithdrawal : Migration
{
    private const string Schema = "speed_reading";
    private const string Settings = "bank_transfer_payment_settings";
    private const string Requests = "bank_transfer_payment_requests";

    private static readonly (string Name, int Length)[] SellerColumns =
    [
        ("SellerTitle", 200), ("SellerAddress", 500), ("SellerTaxOffice", 100), ("SellerTaxNumber", 11),
        ("SellerMersisNumber", 16), ("SellerEmail", 320), ("SellerPhone", 40)
    ];

    private static readonly (string Name, string Type, int? Length)[] RequestColumns =
    [
        ("SalesDocumentsVersion", "character varying(20)", 20), ("SalesDocumentsJson", "text", null),
        ("SalesDocumentsAcceptedAt", "timestamp with time zone", null), ("WithdrawalStatus", "character varying(20)", 20),
        ("WithdrawalReason", "character varying(500)", 500), ("WithdrawalRequestedAt", "timestamp with time zone", null),
        ("WithdrawalReviewNote", "character varying(500)", 500), ("WithdrawalReviewedAt", "timestamp with time zone", null),
        ("WithdrawalReviewedBy", "uuid", null)
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var (name, length) in SellerColumns)
            migrationBuilder.AddColumn<string>(name: name, schema: Schema, table: Settings, type: $"character varying({length})", maxLength: length, nullable: true);
        foreach (var (name, type, length) in RequestColumns)
        {
            if (type == "uuid") migrationBuilder.AddColumn<Guid>(name: name, schema: Schema, table: Requests, type: type, nullable: true);
            else if (type.StartsWith("timestamp", StringComparison.Ordinal)) migrationBuilder.AddColumn<DateTime>(name: name, schema: Schema, table: Requests, type: type, nullable: true);
            else migrationBuilder.AddColumn<string>(name: name, schema: Schema, table: Requests, type: type, maxLength: length, nullable: true);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var (name, _, _) in RequestColumns) migrationBuilder.DropColumn(name: name, schema: Schema, table: Requests);
        foreach (var (name, _) in SellerColumns) migrationBuilder.DropColumn(name: name, schema: Schema, table: Settings);
    }
}
