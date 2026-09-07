using FluentAssertions;
using Identity.Application.BulkUsers;

namespace Identity.API.IntegrationTests;

public sealed class BulkUserCsvTests
{
    [Fact]
    public void Parses_quoted_fields_and_rejects_duplicate_emails()
    {
        var result = IdentityBulkUserCsv.Parse(
            "firstName,lastName,email,phoneNumber,role\n\"Ada, Jr.\",Lovelace,ada@example.com,,Editor\nGrace,Hopper,ada@example.com,,Teacher\n",
            maxRows: 10);

        result.Rows.Should().ContainSingle();
        result.Rows[0].FirstName.Should().Be("Ada, Jr.");
        result.Rows[0].Role.Should().Be("Editor");
        result.Errors.Should().ContainSingle(error => error.Contains("tekrar"));
    }

    [Fact]
    public void Rejects_invalid_headers_and_invalid_email_without_throwing()
    {
        var result = IdentityBulkUserCsv.Parse(
            "name,last,email,phone,role\nAda,Lovelace,not-an-email,,Editor\n",
            maxRows: 10);

        result.Rows.Should().BeEmpty();
        result.Errors.Should().ContainSingle(error => error.Contains("başlıkları"));

        var invalidRow = IdentityBulkUserCsv.Parse(
            "firstName,lastName,email,phoneNumber,role\nAda,Lovelace,not-an-email,,Editor\n",
            maxRows: 10);
        invalidRow.Rows.Should().BeEmpty();
        invalidRow.Errors.Should().Contain(error => error.Contains("e-posta"));
    }

    [Fact]
    public void Rejects_unclosed_quoted_fields()
    {
        var result = IdentityBulkUserCsv.Parse(
            "firstName,lastName,email,phoneNumber,role\n\"Ada,Lovelace,ada@example.com,,Editor\n",
            maxRows: 10);

        result.Rows.Should().BeEmpty();
        result.Errors.Should().ContainSingle(error => error.Contains("kapanmamış tırnak"));
    }

    [Fact]
    public void Escapes_csv_values_and_neutralizes_formula_cells_on_export()
    {
        var csv = IdentityBulkUserCsv.Export([
            new BulkUserCsvExportRow(
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                "=HYPERLINK(\"https://evil.test\")",
                "Ada, Jr.",
                "Lovelace",
                null,
                ["Editor"],
                true,
                false,
                null,
                null)
        ]);

        csv.Should().Contain("'=HYPERLINK(\"\"https://evil.test\"\")");
        csv.Should().Contain("\"Ada, Jr.\"");
    }
}
