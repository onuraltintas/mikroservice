using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography.X509Certificates;

namespace EduPlatform.Shared.Infrastructure.Extensions;

/// <summary>
/// Configures a stable key ring for cookie, antiforgery and other ASP.NET Core
/// protected data. Containers must mount the configured directory as durable
/// storage; local runs use a service-local directory under the application base.
/// </summary>
public static class DataProtectionExtensions
{
    public static IServiceCollection AddPersistentDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        string applicationName,
        bool requireEncryption = false)
    {
        var configuredPath = configuration["DataProtection:KeysPath"];
        var keyPath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "data-protection-keys")
            : configuredPath;

        Directory.CreateDirectory(keyPath);

        var dataProtection = services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
            .SetApplicationName(applicationName);

        var certificatePath = configuration["DataProtection:CertificatePath"];
        var certificatePassword = configuration["DataProtection:CertificatePassword"];
        var isProductionEnvironment = string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"] ?? configuration["ENVIRONMENT"],
            Environments.Production,
            StringComparison.OrdinalIgnoreCase);
        requireEncryption |= isProductionEnvironment;

        if (string.IsNullOrWhiteSpace(certificatePath))
        {
            if (requireEncryption)
            {
                throw new InvalidOperationException(
                    $"DataProtection:CertificatePath is required in Production for {applicationName}.");
            }

            return services;
        }

        if (!File.Exists(certificatePath))
        {
            throw new InvalidOperationException(
                $"Data Protection certificate was not found at '{certificatePath}' for {applicationName}.");
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            certificatePath,
            certificatePassword,
            X509KeyStorageFlags.EphemeralKeySet);

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                $"Data Protection certificate at '{certificatePath}' must include a private key.");
        }

        dataProtection.ProtectKeysWithCertificate(certificate);

        return services;
    }
}
