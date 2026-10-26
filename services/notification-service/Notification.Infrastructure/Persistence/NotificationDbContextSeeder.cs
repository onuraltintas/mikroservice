using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Notification.Infrastructure.Persistence;

public static class NotificationDbContextSeeder
{
    public static async Task SeedAsync(
        NotificationDbContext context,
        ILogger logger,
        bool throwOnError = false)
    {
        try
        {
            logger.LogInformation("🌱 Seeding Notification Templates...");

            // 1. Locate seeds.json
            var baseDir = AppContext.BaseDirectory;
            // seeds.json is copied to the output; during `dotnet run` fall back to the source tree.
            string seedFilePath = Path.Combine(baseDir, "Seed", "seeds.json");
            
            if (!File.Exists(seedFilePath))
            {
               seedFilePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../Notification.Infrastructure/Seed/seeds.json"));
            }

            if (!File.Exists(seedFilePath))
            {
                logger.LogWarning($"⚠️ Seed file not found at: {seedFilePath}. Skipping seeding.");
                if (throwOnError)
                {
                    throw new FileNotFoundException("Notification template seed file was not found.", seedFilePath);
                }

                return;
            }

            var jsonContent = await File.ReadAllTextAsync(seedFilePath);
            var seedData = JsonSerializer.Deserialize<List<SeedItem>>(jsonContent);

            if (seedData == null)
            {
                if (throwOnError)
                {
                    throw new InvalidOperationException("Notification template seed file is empty or invalid.");
                }

                return;
            }

            foreach (var item in seedData)
            {
                // HTML dosyasını oku
                string htmlContent = "";
                string htmlPath = Path.Combine(Path.GetDirectoryName(seedFilePath)!, item.HtmlFile);

                if (File.Exists(htmlPath))
                {
                    htmlContent = await File.ReadAllTextAsync(htmlPath);
                }
                else
                {
                    logger.LogWarning($"⚠️ HTML template not found: {htmlPath}");
                    if (throwOnError)
                    {
                        throw new FileNotFoundException("Notification template HTML file was not found.", htmlPath);
                    }

                    continue;
                }

                // Insert only missing bootstrap templates.
                var existingTemplate = await context.EmailTemplates
                    .OrderBy(t => t.Id)
                    .FirstOrDefaultAsync(t => t.Id == item.Id || t.TemplateName == item.TemplateName);

                if (existingTemplate == null)
                {
                    var newTemplate = new EmailTemplate
                    {
                        Id = item.Id,
                        TemplateName = item.TemplateName,
                        Category = item.Category,
                        Subject = item.Subject,
                        Body = htmlContent,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    context.EmailTemplates.Add(newTemplate);
                }
                else
                {
                    // Seed files provide bootstrap defaults. Preserve templates that
                    // already exist so administrators can safely edit them from the
                    // admin panel without the next deployment overwriting their work.
                    logger.LogDebug(
                        "Notification template {TemplateName} already exists; preserving the stored version.",
                        existingTemplate.TemplateName);
                }
            }

            await context.SaveChangesAsync();
            logger.LogInformation("✅ Notification Templates seeded.");

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error seeding notification templates.");
            if (throwOnError)
            {
                throw;
            }
        }
    }

    private class SeedItem
    {
        public Guid Id { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlFile { get; set; } = string.Empty;
    }
}
