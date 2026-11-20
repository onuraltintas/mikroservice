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
    [InlineData("email,telefon\nada@example.test,555")]
    [InlineData("displayName,displayName\nAda,Ada")]
    [InlineData("ad,Ad Soyad\nAda,Ada")]
    public void RejectsMissingOrDuplicateHeaders(string csv)
    {
        var error = Assert.Throws<StudentCsvImportParseException>(() => StudentCsvImportParser.Parse(csv));
        Assert.Equal("StudentImport.InvalidHeader", error.Code);
    }

    // 20.11.2026 live test: the panel says "ad, e-posta, telefon", and Excel in Turkish saves with semicolons.
    [Theory]
    [InlineData("ad,e-posta,telefon\nAda Öğrenci,ADA@example.test,555 000")]
    [InlineData("Ad Soyad;E-Posta;Telefon\r\nAda Öğrenci;ada@example.test;555 000")]
    [InlineData("ÖĞRENCİ ADI\tE-posta adresi\tCep telefonu\nAda Öğrenci\tada@example.test\t555 000")]
    [InlineData("Sınıf;Adı Soyadı;Veli;Telefon;E-posta\n8-A;Ada Öğrenci;Ayşe;555 000;ada@example.test")]
    public void ReadsTurkishHeadersAndExcelSeparators(string csv)
    {
        var row = Assert.Single(StudentCsvImportParser.Parse(csv).Rows);
        Assert.Null(row.Error);
        Assert.Equal("Ada Öğrenci", row.DisplayName);
        Assert.Equal("ada@example.test", row.Email);
        Assert.Equal("555 000", row.PhoneNumber);
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
