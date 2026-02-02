using Identity.Application.DTOs.Logs;

namespace Identity.Application.Interfaces;

public interface ISystemLogService
{
    Task<PagedLogsResponse> GetLogsAsync(LogFilterRequest request, CancellationToken cancellationToken);
    Task<List<string>> GetApplicationsAsync(CancellationToken cancellationToken);
    
    // Retention Policy Management
    Task<List<RetentionPolicyDto>> GetRetentionPoliciesAsync(CancellationToken cancellationToken);
    Task<RetentionPolicyDto?> CreateRetentionPolicyAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteRetentionPolicyAsync(string policyId, CancellationToken cancellationToken);
    string GetSeqUrl();
}
