using System.Text.Json;

namespace Identity.Application.DTOs.Logs;

public class LogEntryDto
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? Properties { get; set; } // JSON as string
    public string? Application { get; set; }
}

public class LogFilterRequest
{
    public string? Level { get; set; } // Info, Error, Warning
    public string? Application { get; set; } // Identity.API, Coaching.API, etc.
    public string? SearchTerm { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class PagedLogsResponse
{
    public List<LogEntryDto> Logs { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class RetentionPolicyDto
{
    public string Id { get; set; } = string.Empty;
    public string RetentionTime { get; set; } = string.Empty; // e.g., "30.00:00:00" for 30 days
    public JsonElement? RemovedSignalExpression { get; set; } // Can be null or object { Kind: "Signal", ... }
    public string? SignalTitle { get; set; } // Human readable title of the signal
    public int RetentionDays => ParseRetentionDays(RetentionTime);

    private static int ParseRetentionDays(string retentionTime)
    {
        if (string.IsNullOrEmpty(retentionTime)) return 0;
        var parts = retentionTime.Split('.');
        return int.TryParse(parts[0], out var days) ? days : 0;
    }
}

public class CreateRetentionPolicyRequest
{
    public int RetentionDays { get; set; } = 30;
    public string? LogLevel { get; set; } // Information, Warning, Error
}
