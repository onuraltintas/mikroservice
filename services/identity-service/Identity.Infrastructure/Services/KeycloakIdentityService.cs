using System.Net.Http.Json;
using System.Text.Json;
using Identity.Application.Interfaces;
using EduPlatform.Shared.Kernel.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class KeycloakIdentityService : IIdentityService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeycloakIdentityService> _logger;
    private string? _adminToken;
    private DateTime _tokenExpiration;

    public KeycloakIdentityService(
        HttpClient httpClient, 
        IConfiguration configuration,
        ILogger<KeycloakIdentityService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    private async Task<string> GetAdminTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_adminToken) && _tokenExpiration > DateTime.UtcNow.AddMinutes(1))
        {
            return _adminToken;
        }

        var baseUrl = _configuration["Keycloak:BaseUrl"];
        // Admin tasks are done via master realm usually, or the target realm if configured
        // Usually we get admin token from 'master' realm to manage other realms, 
        // OR we use the admin-cli client of the specific realm if it has permissions.
        // For simplicity, let's assume we use the admin user of the master realm or the specific realm.
        // If we use 'admin' user, it's usually in 'master'.
        
        // Let's use the configuration. If AdminUsername is provided, we use password grant.
        var tokenUrl = $"{baseUrl}/realms/master/protocol/openid-connect/token";

        var requestBody = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", "admin-cli"),
            new KeyValuePair<string, string>("username", _configuration["Keycloak:AdminUsername"]!),
            new KeyValuePair<string, string>("password", _configuration["Keycloak:AdminPassword"]!),
            new KeyValuePair<string, string>("grant_type", "password")
        });

        var response = await _httpClient.PostAsync(tokenUrl, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        _adminToken = content.GetProperty("access_token").GetString()!;
        var expiresIn = content.GetProperty("expires_in").GetInt32();
        _tokenExpiration = DateTime.UtcNow.AddSeconds(expiresIn);

        return _adminToken;
    }

    public async Task<Result<Guid>> RegisterUserAsync(string email, string password, string firstName, string lastName, CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var baseUrl = _configuration["Keycloak:BaseUrl"];
            var targetRealm = _configuration["Keycloak:Realm"];
            var createUserUrl = $"{baseUrl}/admin/realms/{targetRealm}/users";

            var newUser = new
            {
                username = email,
                email = email,
                firstName = firstName,
                lastName = lastName,
                enabled = true,
                credentials = new[]
                {
                    new { type = "password", value = password, temporary = false }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(createUserUrl, newUser, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // User created. Now we need to get the ID.
                // Location header might contain the ID: Location: .../users/{id}
                if (response.Headers.Location != null)
                {
                    var path = response.Headers.Location.AbsolutePath;
                    var id = path.Substring(path.LastIndexOf('/') + 1);
                    if (Guid.TryParse(id, out var guid))
                    {
                        return guid;
                    }
                }

                // If location header is missing or parse fails, search by email
                var searchUrl = $"{createUserUrl}?email={email}&exact=true";
                var searchResponse = await _httpClient.GetFromJsonAsync<JsonElement[]>(searchUrl, cancellationToken);
                if (searchResponse != null && searchResponse.Length > 0)
                {
                    var idStr = searchResponse[0].GetProperty("id").GetString();
                    return Guid.Parse(idStr!);
                }
                
                return Result.Failure<Guid>(new Error("Keycloak.UserCreatedButIdNotFound", "User created but ID could not be retrieved."));
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Keycloak Registration Failed: {Error}", errorContent);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                     return Result.Failure<Guid>(new Error("User.Duplicate", "User with this email already exists."));
                }

                return Result.Failure<Guid>(new Error("Keycloak.RegistrationFailed", $"Keycloak returned {response.StatusCode}"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user in Keycloak");
            return Result.Failure<Guid>(new Error("Keycloak.Error", ex.Message));
        }
    }

    public async Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var baseUrl = _configuration["Keycloak:BaseUrl"];
            var targetRealm = _configuration["Keycloak:Realm"];
            var deleteUrl = $"{baseUrl}/admin/realms/{targetRealm}/users/{userId}";

            var response = await _httpClient.DeleteAsync(deleteUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            return Result.Failure(new Error("Keycloak.DeleteFailed", $"Failed to delete user. Status: {response.StatusCode}"));
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("Keycloak.Error", ex.Message));
        }
    }

    public async Task<Result> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await UpdateUserStatusAsync(userId, false, cancellationToken);
    }

    public async Task<Result> ActivateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await UpdateUserStatusAsync(userId, true, cancellationToken);
    }

    private async Task<Result> UpdateUserStatusAsync(Guid userId, bool enabled, CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var baseUrl = _configuration["Keycloak:BaseUrl"];
            var targetRealm = _configuration["Keycloak:Realm"];
            var updateUrl = $"{baseUrl}/admin/realms/{targetRealm}/users/{userId}";

            var updateUser = new
            {
                enabled = enabled,
                emailVerified = enabled // If activating, verify email. If deactivating, unverify.
            };

            var response = await _httpClient.PutAsJsonAsync(updateUrl, updateUser, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Keycloak Update Status Failed: {Error}", errorContent);
            return Result.Failure(new Error("Keycloak.UpdateStatusFailed", $"Failed to update user status. Status: {response.StatusCode}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user status in Keycloak");
            return Result.Failure(new Error("Keycloak.Error", ex.Message));
        }
    }

    public async Task<Result> AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var baseUrl = _configuration["Keycloak:BaseUrl"];
            var targetRealm = _configuration["Keycloak:Realm"];

            // 1. Get Role ID by Name
            var roleUrl = $"{baseUrl}/admin/realms/{targetRealm}/roles/{roleName}";
            var roleResponse = await _httpClient.GetAsync(roleUrl, cancellationToken);
            
            if (!roleResponse.IsSuccessStatusCode)
            {
                 // Try creating role if not exists? No, roles should be pre-created or handled separately.
                 return Result.Failure(new Error("Keycloak.RoleNotFound", $"Role '{roleName}' not found in Keycloak realm."));
            }

            var roleContent = await roleResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            
            // 2. Assign Role to User
            var assignUrl = $"{baseUrl}/admin/realms/{targetRealm}/users/{userId}/role-mappings/realm";
            
            // Keycloak API expects an array of RoleRepresentation
            var rolesToAssign = new[] { roleContent };

            var assignResponse = await _httpClient.PostAsJsonAsync(assignUrl, rolesToAssign, cancellationToken);

            if (assignResponse.IsSuccessStatusCode)
            {
                return Result.Success();
            }

            var errorContent = await assignResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Keycloak Role Assignment Failed: {Error}", errorContent);
            return Result.Failure(new Error("Keycloak.RoleAssignmentFailed", $"Failed to assign role. Status: {assignResponse.StatusCode}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role in Keycloak");
            return Result.Failure(new Error("Keycloak.Error", ex.Message));
        }
    }

    /// <summary>
    /// Creates a user with a temporary password that must be changed on first login.
    /// Returns the UserId and the generated temporary password.
    /// </summary>
    public async Task<Result<(Guid UserId, string TemporaryPassword)>> RegisterUserWithTemporaryPasswordAsync(
        string email, 
        string firstName, 
        string lastName, 
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await GetAdminTokenAsync(cancellationToken);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var baseUrl = _configuration["Keycloak:BaseUrl"];
            var targetRealm = _configuration["Keycloak:Realm"];
            var createUserUrl = $"{baseUrl}/admin/realms/{targetRealm}/users";

            // Generate a random temporary password
            var tempPassword = GenerateTemporaryPassword();

            var newUser = new
            {
                username = email,
                email = email,
                firstName = firstName,
                lastName = lastName,
                enabled = true,
                credentials = new[]
                {
                    new { type = "password", value = tempPassword, temporary = true }
                },
                requiredActions = new[] { "UPDATE_PASSWORD" }
            };

            var response = await _httpClient.PostAsJsonAsync(createUserUrl, newUser, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                Guid userId;
                
                if (response.Headers.Location != null)
                {
                    var path = response.Headers.Location.AbsolutePath;
                    var id = path.Substring(path.LastIndexOf('/') + 1);
                    if (Guid.TryParse(id, out userId))
                    {
                        return (userId, tempPassword);
                    }
                }

                // Fallback: search by email
                var searchUrl = $"{createUserUrl}?email={email}&exact=true";
                var searchResponse = await _httpClient.GetFromJsonAsync<JsonElement[]>(searchUrl, cancellationToken);
                if (searchResponse != null && searchResponse.Length > 0)
                {
                    var idStr = searchResponse[0].GetProperty("id").GetString();
                    userId = Guid.Parse(idStr!);
                    return (userId, tempPassword);
                }
                
                return Result.Failure<(Guid, string)>(new Error("Keycloak.UserCreatedButIdNotFound", "User created but ID could not be retrieved."));
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Keycloak Registration Failed: {Error}", errorContent);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                     return Result.Failure<(Guid, string)>(new Error("User.Duplicate", "User with this email already exists."));
                }

                return Result.Failure<(Guid, string)>(new Error("Keycloak.RegistrationFailed", $"Keycloak returned {response.StatusCode}"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user with temporary password in Keycloak");
            return Result.Failure<(Guid, string)>(new Error("Keycloak.Error", ex.Message));
        }
    }

    private static string GenerateTemporaryPassword()
    {
        const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@#$%^&*";
        
        var random = new Random();
        var password = new char[12];
        
        // Ensure at least one of each type
        password[0] = upperCase[random.Next(upperCase.Length)];
        password[1] = lowerCase[random.Next(lowerCase.Length)];
        password[2] = digits[random.Next(digits.Length)];
        password[3] = special[random.Next(special.Length)];
        
        // Fill the rest randomly
        var allChars = upperCase + lowerCase + digits + special;
        for (int i = 4; i < password.Length; i++)
        {
            password[i] = allChars[random.Next(allChars.Length)];
        }
        
        // Shuffle
        return new string(password.OrderBy(_ => random.Next()).ToArray());
    }
}
