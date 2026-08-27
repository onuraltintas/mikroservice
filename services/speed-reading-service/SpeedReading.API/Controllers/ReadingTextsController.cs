using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/reading-texts")]
[Authorize]
public sealed class ReadingTextsController(
    ILegacySpeedReadingCatalog catalog,
    ISpeedReadingCatalogAdminWriter adminWriter,
    ISpeedReadingReadingTextExporter exporter) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ReadingTextSummary>> GetReadingTexts(
        [FromQuery] Guid? exerciseId,
        [FromQuery] string? category,
        [FromQuery] int? difficultyLevel,
        [FromQuery] string? searchTerm,
        [FromQuery] Guid? targetAgeGroupId,
        [FromQuery] bool? isActive,
        [FromQuery] bool onlyWithQuestions = false,
        CancellationToken cancellationToken = default)
    {
        var canManageContent = User.Claims.Any(claim =>
            claim.Type == "permission" &&
            claim.Value == PlatformPermissions.SpeedReading.ContentManage);
        var effectiveIsActive = canManageContent ? isActive : true;

        return catalog.GetReadingTextsAsync(
            exerciseId,
            category,
            difficultyLevel,
            searchTerm,
            onlyWithQuestions,
            targetAgeGroupId,
            effectiveIsActive,
            cancellationToken);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(
        CancellationToken cancellationToken = default) =>
        Ok(await catalog.GetReadingTextCategoriesAsync(cancellationToken));

    [HttpGet("levels")]
    public async Task<ActionResult<IReadOnlyList<int>>> GetLevels(
        CancellationToken cancellationToken = default) =>
        Ok(await catalog.GetReadingTextDifficultyLevelsAsync(cancellationToken));

    [HttpGet("short")]
    public async Task<ActionResult<IReadOnlyList<ShortReadingTextSummary>>> GetShortReadingTexts(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default) =>
        Ok(await catalog.GetShortReadingTextsAsync(limit, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReadingTextDetails>> GetReadingText(
        Guid id,
        [FromQuery] bool includeQuestions = true,
        CancellationToken cancellationToken = default)
    {
        var canManageContent = User.Claims.Any(claim =>
            claim.Type == "permission" &&
            claim.Value == PlatformPermissions.SpeedReading.ContentManage);
        var result = await catalog.GetReadingTextAsync(
            id,
            includeQuestions,
            canManageContent,
            canManageContent,
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<ActionResult<ReadingTextSummary>> CreateReadingText(
        [FromBody] CreateReadingTextRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.CreateReadingTextAsync(
            actorId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<ActionResult<ReadingTextSummary>> UpdateReadingText(
        Guid id,
        [FromBody] UpdateReadingTextRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.UpdateReadingTextAsync(
            actorId,
            id,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> DeleteReadingText(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.DeleteReadingTextAsync(
            actorId,
            id,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/export/pdf")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> ExportToPdf(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var text = await catalog.GetReadingTextAsync(
            id,
            includeQuestions: true,
            includeInactive: true,
            includeAnswers: true,
            cancellationToken);
        return text is null
            ? NotFound()
            : File(exporter.GeneratePdf(text), "application/pdf", $"{SanitizeFileName(text.Title)}.pdf");
    }

    [HttpGet("{id:guid}/export/docx")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> ExportToDocx(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var text = await catalog.GetReadingTextAsync(
            id,
            includeQuestions: true,
            includeInactive: true,
            includeAnswers: true,
            cancellationToken);
        return text is null
            ? NotFound()
            : File(
                exporter.GenerateDocx(text),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                $"{SanitizeFileName(text.Title)}.docx");
    }

    [HttpPost("export/pdf")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> ExportMultipleToPdf(
        [FromBody] ExportReadingTextsRequest? request,
        CancellationToken cancellationToken = default)
    {
        var texts = await GetExportTextsAsync(request, cancellationToken);
        if (texts is null)
        {
            return BadRequest(new { message = $"Select between 1 and {MaxExportItems} reading texts." });
        }

        return File(
            exporter.GenerateMultiplePdf(texts),
            "application/pdf",
            $"OkumaMetinleri_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf");
    }

    [HttpPost("export/docx")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> ExportMultipleToDocx(
        [FromBody] ExportReadingTextsRequest? request,
        CancellationToken cancellationToken = default)
    {
        var texts = await GetExportTextsAsync(request, cancellationToken);
        if (texts is null)
        {
            return BadRequest(new { message = $"Select between 1 and {MaxExportItems} reading texts." });
        }

        return File(
            exporter.GenerateMultipleDocx(texts),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"OkumaMetinleri_{DateTime.UtcNow:yyyyMMdd_HHmmss}.docx");
    }

    [HttpPost("import/bulk")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> ImportBulk(
        [FromBody] IReadOnlyList<ImportReadingTextRequest>? requests,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        if (requests is null || requests.Count == 0 || requests.Count > MaxImportRows)
        {
            return BadRequest(new { message = $"Import must contain between 1 and {MaxImportRows} rows." });
        }

        var serialized = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(requests);
        var rows = requests.Select(request => new ParsedImportRow(
            request.Title,
            request.Content,
            request.DifficultyLevel,
            request.Category,
            request.Language ?? "tr",
            request.Questions ?? [])).ToList();
        return await ImportRowsAsync(
            actorId,
            rows,
            ResolveImportKey(idempotencyKey, serialized),
            cancellationToken);
    }

    [HttpPost("import/csv")]
    [Consumes("multipart/form-data")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    [RequestSizeLimit(MaxImportFileBytes)]
    public async Task<IActionResult> ImportCsv(
        IFormFile? file,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await ImportFileAsync(file, false, idempotencyKey, cancellationToken);
    }

    [HttpPost("import/excel")]
    [Consumes("multipart/form-data")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    [RequestSizeLimit(MaxImportFileBytes)]
    public async Task<IActionResult> ImportExcel(
        IFormFile? file,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await ImportFileAsync(file, true, idempotencyKey, cancellationToken);
    }

    private const int MaxImportRows = 500;
    private const long MaxImportFileBytes = 10 * 1024 * 1024;
    private const int MaxExportItems = 100;

    private async Task<IReadOnlyList<ReadingTextDetails>?> GetExportTextsAsync(
        ExportReadingTextsRequest? request,
        CancellationToken cancellationToken)
    {
        var ids = request?.Ids?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids is null || ids.Count == 0 || ids.Count > MaxExportItems)
        {
            return null;
        }

        var texts = new List<ReadingTextDetails>(ids.Count);
        foreach (var id in ids)
        {
            var text = await catalog.GetReadingTextAsync(
                id,
                includeQuestions: true,
                includeInactive: true,
                includeAnswers: true,
                cancellationToken);
            if (text is null)
            {
                return null;
            }

            texts.Add(text);
        }

        return texts;
    }

    private static string SanitizeFileName(string title)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeTitle = new string(title.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safeTitle) ? "reading-text" : safeTitle;
    }

    private async Task<IActionResult> ImportFileAsync(
        IFormFile? file,
        bool excel,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0 || file.Length > MaxImportFileBytes)
        {
            return BadRequest(new { message = "A non-empty import file up to 10 MB is required." });
        }

        var expectedExtension = excel ? ".xlsx" : ".csv";
        if (!Path.GetExtension(file.FileName).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"Only {expectedExtension} files are supported." });
        }

        await using var input = file.OpenReadStream();
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        List<ParsedImportRow> rows;
        try
        {
            rows = excel ? ParseExcel(bytes) : ParseCsv(Encoding.UTF8.GetString(bytes));
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        if (rows.Count == 0 || rows.Count > MaxImportRows)
        {
            return BadRequest(new { message = $"Import must contain between 1 and {MaxImportRows} rows." });
        }

        return await ImportRowsAsync(
            actorId,
            rows,
            ResolveImportKey(idempotencyKey, bytes),
            cancellationToken);
    }

    private async Task<IActionResult> ImportRowsAsync(
        Guid actorId,
        IReadOnlyList<ParsedImportRow> rows,
        string importKey,
        CancellationToken cancellationToken)
    {
        var errors = new string?[rows.Count];
        var successCount = 0;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            try
            {
                var text = await adminWriter.CreateReadingTextAsync(
                    actorId,
                    new CreateReadingTextRequest(
                        row.Title,
                        row.Content,
                        row.Content.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length,
                        row.Category,
                        row.DifficultyLevel,
                        null,
                        row.Language,
                        true,
                        null,
                        1,
                        10,
                        null),
                    $"{importKey}-{index + 1}",
                    cancellationToken);

                for (var questionIndex = 0; questionIndex < row.Questions.Count; questionIndex++)
                {
                    var question = row.Questions[questionIndex];
                    await adminWriter.CreateReadingQuestionAsync(
                        actorId,
                        new CreateReadingQuestionRequest(
                            text.Id,
                            question.QuestionText,
                            question.Type,
                            1,
                            1,
                            null,
                            question.OptionA,
                            question.OptionB,
                            question.OptionC,
                            question.OptionD,
                            question.CorrectAnswer,
                            questionIndex),
                        $"{importKey}-{index + 1}-q-{questionIndex + 1}",
                        cancellationToken);
                }

                successCount++;
            }
            catch (ArgumentException)
            {
                errors[index] = $"Row {index + 1} contains invalid fields.";
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                errors[index] = $"Row {index + 1} could not be imported.";
            }
        }

        return Ok(new
        {
            successCount,
            errorCount = rows.Count - successCount,
            errors
        });
    }

    private static List<ParsedImportRow> ParseCsv(string content)
    {
        var records = ParseCsvRecords(content);
        if (records.Count < 2)
        {
            throw new InvalidDataException("CSV file must contain a header and at least one data row.");
        }

        return ParseRows(records);
    }

    private static List<ParsedImportRow> ParseExcel(byte[] bytes)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var worksheet = archive.GetEntry("xl/worksheets/sheet1.xml")
                ?? throw new InvalidDataException("The XLSX workbook does not contain a first worksheet.");
            var sharedStrings = ReadSharedStrings(archive);
            using var worksheetStream = worksheet.Open();
            var document = XDocument.Load(worksheetStream);
            var ns = document.Root?.Name.Namespace
                ?? throw new InvalidDataException("The XLSX worksheet is invalid.");
            var records = document.Descendants(ns + "row")
                .Select(row => ReadExcelRow(row, ns, sharedStrings))
                .ToList();
            if (records.Count < 2)
            {
                throw new InvalidDataException("Excel file must contain a header and at least one data row.");
            }

            return ParseRows(records);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or XmlException)
        {
            throw new InvalidDataException("The Excel file could not be read.", exception);
        }
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;
        return document.Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToList();
    }

    private static string ReadExcelCell(
        XElement cell,
        XNamespace ns,
        IReadOnlyList<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(ns + "t").Select(text => text.Value));
        }

        var value = cell.Element(ns + "v")?.Value ?? string.Empty;
        return type == "s" && int.TryParse(value, out var sharedIndex)
            && sharedIndex >= 0 && sharedIndex < sharedStrings.Count
            ? sharedStrings[sharedIndex]
            : value;
    }

    private static List<string> ReadExcelRow(
        XElement row,
        XNamespace ns,
        IReadOnlyList<string> sharedStrings)
    {
        var values = new List<string>();
        foreach (var cell in row.Elements(ns + "c"))
        {
            var reference = cell.Attribute("r")?.Value ?? string.Empty;
            var columnIndex = GetExcelColumnIndex(reference);
            while (values.Count <= columnIndex)
            {
                values.Add(string.Empty);
            }

            values[columnIndex] = ReadExcelCell(cell, ns, sharedStrings);
        }

        return values;
    }

    private static int GetExcelColumnIndex(string reference)
    {
        var index = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter))
        {
            index = index * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        }

        return Math.Max(index - 1, 0);
    }

    private static List<ParsedImportRow> ParseRows(IReadOnlyList<List<string>> records)
    {
        var headers = records[0]
            .Select(NormalizeHeader)
            .ToList();
        var result = new List<ParsedImportRow>(records.Count - 1);
        foreach (var record in records.Skip(1))
        {
            var values = headers
                .Select((header, index) => (header, value: index < record.Count ? record[index].Trim() : string.Empty))
                .GroupBy(item => item.header, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().value, StringComparer.OrdinalIgnoreCase);
            var levelValue = GetValue(values, "level", "difficultylevel", "difficulty");
            if (!int.TryParse(levelValue, out var level))
            {
                level = 1;
            }

            var questions = new List<ImportQuestionRequest>();
            for (var questionIndex = 1; questionIndex <= 10; questionIndex++)
            {
                var prefix = $"q{questionIndex}";
                var questionText = GetValue(values, $"{prefix}text");
                if (string.IsNullOrWhiteSpace(questionText))
                {
                    continue;
                }

                _ = int.TryParse(GetValue(values, $"{prefix}type"), out var type);
                questions.Add(new ImportQuestionRequest(
                    questionText,
                    type is >= 1 and <= 3 ? type : 1,
                    GetValue(values, $"{prefix}a"),
                    GetValue(values, $"{prefix}b"),
                    GetValue(values, $"{prefix}c"),
                    GetValue(values, $"{prefix}d"),
                    GetValue(values, $"{prefix}correct")));
            }

            result.Add(new ParsedImportRow(
                GetValue(values, "title", "baslik"),
                GetValue(values, "content", "icerik"),
                level,
                GetValue(values, "category", "kategori"),
                GetValue(values, "language", "dil") is { Length: > 0 } language ? language : "tr",
                questions));
        }

        return result;
    }

    private static List<List<string>> ParseCsvRecords(string content)
    {
        var records = new List<List<string>>();
        var currentRecord = new List<string>();
        var currentValue = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '"')
            {
                if (quoted && index + 1 < content.Length && content[index + 1] == '"')
                {
                    currentValue.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                currentRecord.Add(currentValue.ToString());
                currentValue.Clear();
            }
            else if ((character == '\n' || character == '\r') && !quoted)
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                currentRecord.Add(currentValue.ToString());
                currentValue.Clear();
                if (currentRecord.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    records.Add(currentRecord);
                }
                currentRecord = new List<string>();
            }
            else
            {
                currentValue.Append(character);
            }
        }

        if (quoted)
        {
            throw new InvalidDataException("CSV contains an unterminated quoted value.");
        }

        if (currentValue.Length > 0 || currentRecord.Count > 0)
        {
            currentRecord.Add(currentValue.ToString());
            if (currentRecord.Any(value => !string.IsNullOrWhiteSpace(value)))
            {
                records.Add(currentRecord);
            }
        }

        return records;
    }

    private static string GetValue(IReadOnlyDictionary<string, string> values, params string[] keys) =>
        keys.Select(key => values.GetValueOrDefault(key, string.Empty))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string NormalizeHeader(string value) =>
        value.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);

    private static string ResolveImportKey(string? candidate, byte[] content)
    {
        if (!string.IsNullOrWhiteSpace(candidate)
            && candidate.Trim().Length is >= 16 and <= 80
            && candidate.Trim().All(character => char.IsLetterOrDigit(character) || "._~-".Contains(character)))
        {
            return candidate.Trim();
        }

        return $"speed-reading-import-{Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()[..32]}";
    }

    private sealed record ParsedImportRow(
        string Title,
        string Content,
        int DifficultyLevel,
        string Category,
        string Language,
        IReadOnlyList<ImportQuestionRequest> Questions);

    public sealed record ImportReadingTextRequest(
        string Title,
        string Content,
        int DifficultyLevel,
        string Category,
        string? Language,
        IReadOnlyList<ImportQuestionRequest>? Questions);

    public sealed record ImportQuestionRequest(
        string QuestionText,
        int Type,
        string OptionA,
        string OptionB,
        string OptionC,
        string OptionD,
        string CorrectAnswer);

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out userId);
    }
}

public sealed record ExportReadingTextsRequest(IReadOnlyList<Guid> Ids);
