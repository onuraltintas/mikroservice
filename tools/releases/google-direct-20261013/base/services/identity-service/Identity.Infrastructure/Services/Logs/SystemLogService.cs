using Identity.Application.DTOs.Logs;
using Identity.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Identity.Infrastructure.Services.Logs;

public class SystemLogService : ISystemLogService
{
    private readonly HttpClient _httpClient;
    private readonly string _seqUrl;

    public SystemLogService(IConfiguration configuration)
    {
        _seqUrl = Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://localhost:5341";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_seqUrl)
        };
    }

    public async Task<PagedLogsResponse> GetLogsAsync(LogFilterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Build Seq query filter
            var filter = BuildSeqFilter(request);
            var count = request.PageSize;

            // Seq Events API
            var url = $"/api/events?count={count}";
            if (!string.IsNullOrEmpty(filter))
            {
                url += $"&filter={Uri.EscapeDataString(filter)}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new PagedLogsResponse
                {
                    Logs = new List<LogEntryDto>(),
                    TotalCount = 0,
                    Page = request.Page,
                    PageSize = request.PageSize
                };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var events = JsonSerializer.Deserialize<List<SeqEvent>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<SeqEvent>();

            var logs = events.Select(MapToLogEntry).ToList();

            // Get total count
            var totalCount = await GetTotalCountAsync(filter, cancellationToken);

            return new PagedLogsResponse
            {
                Logs = logs,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SystemLogService Error: {ex.Message}");
            return new PagedLogsResponse
            {
                Logs = new List<LogEntryDto>(),
                TotalCount = 0,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
    }

    private LogEntryDto MapToLogEntry(SeqEvent e)
    {
        // Extract Application from Properties array
        string? application = null;
        string? propertiesJson = null;

        if (e.Properties != null && e.Properties.Count > 0)
        {
            var appProp = e.Properties.FirstOrDefault(p => p.Name == "Application");
            if (appProp != null)
            {
                application = appProp.Value?.ToString();
            }

            // Convert properties to key-value JSON
            var propsDict = e.Properties.ToDictionary(p => p.Name ?? "", p => p.Value);
            propertiesJson = JsonSerializer.Serialize(propsDict);
        }

        // Build rendered message from template tokens
        var message = e.RenderedMessage;
        if (string.IsNullOrEmpty(message) && e.MessageTemplateTokens != null)
        {
            message = string.Join("", e.MessageTemplateTokens.Select(t => 
                !string.IsNullOrEmpty(t.FormattedValue) ? t.FormattedValue :
                !string.IsNullOrEmpty(t.Text) ? t.Text :
                $"{{{t.PropertyName}}}")
            );
        }

        return new LogEntryDto
        {
            Timestamp = e.Timestamp,
            Level = e.Level ?? "Information",
            Message = message ?? "",
            Exception = e.Exception,
            Properties = propertiesJson,
            Application = application
        };
    }

    private string BuildSeqFilter(LogFilterRequest request)
    {
        var conditions = new List<string>();

        if (!string.IsNullOrEmpty(request.Level))
        {
            conditions.Add($"@Level = \"{request.Level}\"");
        }

        if (!string.IsNullOrEmpty(request.Application))
        {
            conditions.Add($"Application = \"{request.Application}\"");
        }

        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            // Seq uses Contains() or IndexOf() for text search
            // Search in Message, Exception, and Application properties
            var term = request.SearchTerm.Replace("\"", "\\\"");
            conditions.Add($"(IndexOf(@Message, \"{term}\") >= 0 or IndexOf(@Exception, \"{term}\") >= 0)");
        }

        if (request.StartDate.HasValue)
        {
            conditions.Add($"@Timestamp >= DateTime(\"{request.StartDate.Value:yyyy-MM-ddTHH:mm:ss}\")");
        }

        if (request.EndDate.HasValue)
        {
            conditions.Add($"@Timestamp <= DateTime(\"{request.EndDate.Value:yyyy-MM-ddTHH:mm:ss}\")");
        }

        return conditions.Count > 0 ? string.Join(" and ", conditions) : "";
    }

    public async Task<List<string>> GetApplicationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get recent events to extract unique applications
            var url = "/api/events?count=500";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new List<string>();
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var events = JsonSerializer.Deserialize<List<SeqEvent>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<SeqEvent>();

            var applications = events
                .SelectMany(e => e.Properties ?? new List<SeqProperty>())
                .Where(p => p.Name == "Application" && p.Value != null)
                .Select(p => p.Value?.ToString() ?? "")
                .Where(a => !string.IsNullOrEmpty(a))
                .Distinct()
                .OrderBy(a => a)
                .ToList();

            return applications;
        }
        catch
        {
            return new List<string>();
        }
    }

    public string GetSeqUrl()
    {
        return _seqUrl;
    }

    public async Task<List<RetentionPolicyDto>> GetRetentionPoliciesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/retentionpolicies", cancellationToken);
            if (!response.IsSuccessStatusCode) return new List<RetentionPolicyDto>();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var policies = JsonSerializer.Deserialize<List<RetentionPolicyDto>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<RetentionPolicyDto>();

            // Enrich with signal names if needed
            if (policies.Any(p => p.RemovedSignalExpression.HasValue))
            {
                var signalsResponse = await _httpClient.GetAsync("/api/signals?shared=true", cancellationToken);
                if (signalsResponse.IsSuccessStatusCode)
                {
                    var signalsContent = await signalsResponse.Content.ReadAsStringAsync(cancellationToken);
                    var signals = JsonSerializer.Deserialize<List<SeqSignal>>(signalsContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (signals != null)
                    {
                        foreach (var policy in policies)
                        {
                            if (policy.RemovedSignalExpression.HasValue)
                            {
                                try 
                                {
                                    var elem = policy.RemovedSignalExpression.Value;
                                    // Try case-insensitive property access for SignalId
                                    string? signalId = null;
                                    if (elem.TryGetProperty("SignalId", out var prop) || elem.TryGetProperty("signalId", out prop))
                                    {
                                        signalId = prop.GetString();
                                    }

                                    if (!string.IsNullOrEmpty(signalId))
                                    {
                                        var signal = signals.FirstOrDefault(s => s.Id == signalId);
                                        if (signal != null)
                                        {
                                            // Make it prettier: "Auto-Retention: Information" -> "Information"
                                            policy.SignalTitle = signal.Title?.Replace("Auto-Retention: ", "") ?? signal.Title;
                                        }
                                    }
                                } 
                                catch { /* Ignore parsing errors */ }
                            }
                        }
                    }
                }
            }

            return policies;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching retention policies: {ex.Message}");
            return new List<RetentionPolicyDto>();
        }
    }

    public async Task<RetentionPolicyDto?> CreateRetentionPolicyAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            object? removedSignalExpression = null;

            if (!string.IsNullOrEmpty(request.LogLevel))
            {
                var signalId = await GetSignalIdForLevelAsync(request.LogLevel, cancellationToken);
                if (signalId != null)
                {
                    removedSignalExpression = new { Kind = "Signal", SignalId = signalId };
                }
            }

            var retentionTime = $"{request.RetentionDays}.00:00:00";
            var policy = new
            {
                RetentionTime = retentionTime,
                RemovedSignalExpression = removedSignalExpression
            };

            var json = JsonSerializer.Serialize(policy);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/retentionpolicies", content, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<RetentionPolicyDto>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
             Console.WriteLine($"Error creating retention policy: {ex.Message}");
             return null;
        }
    }

    private async Task<string?> GetSignalIdForLevelAsync(string level, CancellationToken cancellationToken)
    {
        try 
        {
            var response = await _httpClient.GetAsync("/api/signals?shared=true", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var signals = JsonSerializer.Deserialize<List<SeqSignal>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                var existing = signals?.FirstOrDefault(s => string.Equals(s.Title, $"Auto-Retention: {level}", StringComparison.OrdinalIgnoreCase));
                if (existing != null) return existing.Id;
            }

            // Create new
            var newSignal = new
            {
                Title = $"Auto-Retention: {level}",
                Description = "Created by Admin Panel for retention policy",
                Filters = new[]
                {
                    new { Filter = $"@Level = '{level}'" }
                }
            };

            var json = JsonSerializer.Serialize(newSignal);
            var createResponse = await _httpClient.PostAsync("/api/signals", new StringContent(json, System.Text.Encoding.UTF8, "application/json"), cancellationToken);
            
            if (createResponse.IsSuccessStatusCode)
            {
                var content = await createResponse.Content.ReadAsStringAsync(cancellationToken);
                var created = JsonSerializer.Deserialize<SeqSignal>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return created?.Id;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error managing signals: {ex.Message}");
        }
        return null;
    }

    private class SeqSignal
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
    }

    public async Task<bool> DeleteRetentionPolicyAsync(string policyId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/retentionpolicies/{policyId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting retention policy: {ex.Message}");
            return false;
        }
    }

    private async Task<int> GetTotalCountAsync(string filter, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"/api/events?count=10000";
            if (!string.IsNullOrEmpty(filter))
            {
                url += $"&filter={Uri.EscapeDataString(filter)}";
            }
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var events = JsonSerializer.Deserialize<List<object>>(content);
                return events?.Count ?? 0;
            }
        }
        catch { }
        
        return 0;
    }

    // Seq Event structure
    private class SeqEvent
    {
        public string? Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Level { get; set; }
        public string? MessageTemplate { get; set; }
        public string? RenderedMessage { get; set; }
        public string? Exception { get; set; }
        public List<SeqProperty>? Properties { get; set; }
        public List<SeqTemplateToken>? MessageTemplateTokens { get; set; }
    }

    private class SeqProperty
    {
        public string? Name { get; set; }
        public object? Value { get; set; }
    }

    private class SeqTemplateToken
    {
        public string? Text { get; set; }
        public string? PropertyName { get; set; }
        public string? FormattedValue { get; set; }
    }
}
