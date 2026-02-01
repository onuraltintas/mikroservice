using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Notification.Application.Commands.SubmitSupportRequest;
using Notification.Application.Interfaces;

namespace Notification.Infrastructure.ExternalServices;

public class IdentityInternalService : IIdentityInternalService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public IdentityInternalService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
    }

    public async Task ForwardSupportRequestAsync(SubmitSupportRequestCommand request, Guid supportRequestId)
    {
        var command = new
        {
            SupportRequestId = supportRequestId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Subject,
            request.Message
        };

        try
        {
            await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/internal/notification/forward-support", command);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the main process
            Console.WriteLine($"Error forwarding support request to Identity Service: {ex.Message}");
        }
    }
}
