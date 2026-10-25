using CourseManagement.Application;

namespace CourseManagement.Application.UnitTests;

public sealed class StudentCsvImportParserTests
{
    [Fact]
    public void ParsesUtf8BomQuotedValuesAndHeaderOrder()
    {
        var result = StudentCsvImportParser.Parse("\uFEFFemail,phoneNumber,displayName\r\nADA@EXAMPLE.TEST, +90 555 000 00 00,\"Ada, Öğrenci\"");

        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("Ada, Öğrenci", row.DisplayName);
        Assert.Equal("ada@example.test", row.Email);
        Assert.Equal("+90 555 000 00 00", row.PhoneNumber);
        Assert.Null(row.Error);
    }

    [Fact]
    public void ReportsInvalidDataRowsWithoutDroppingOtherRows()
    {
        var result = StudentCsvImportParser.Parse("displayName,email,phoneNumber\nAyşe,ayse@example.test,\n,broken@example.test,\nMehmet,too,many,columns");

        Assert.Equal(3, result.Rows.Count);
        Assert.Null(result.Rows[0].Error);
        Assert.Contains("ad", result.Rows[1].Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sütun", result.Rows[2].Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("name,email\nAda,ada@example.test")]
    [InlineData("displayName,displayName\nAda,Ada")]
    public void RejectsMissingOrDuplicateHeaders(string csv)
    {
        var error = Assert.Throws<StudentCsvImportParseException>(() => StudentCsvImportParser.Parse(csv));
        Assert.Equal("StudentImport.InvalidHeader", error.Code);
    }

    [Fact]
    public void RejectsOversizedFilesAndRows()
    {
        var oversized = Assert.Throws<StudentCsvImportParseException>(() =>
            StudentCsvImportParser.Parse(new string('x', StudentCsvImportParser.MaximumCsvLength + 1)));
        Assert.Equal("StudentImport.FileTooLarge", oversized.Code);

        var rows = "displayName\n" + string.Join('\n', Enumerable.Repeat("Ada", StudentCsvImportParser.MaximumRows + 1));
        var tooMany = Assert.Throws<StudentCsvImportParseException>(() => StudentCsvImportParser.Parse(rows));
        Assert.Equal("StudentImport.TooManyRows", tooMany.Code);
    }
}
