using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Notification.Infrastructure.Persistence;

public static class NotificationDbContextSeeder
{
    public static async Task SeedAsync(NotificationDbContext context, ILogger logger)
    {
        try
        {
            logger.LogInformation("🌱 Seeding Notification Templates...");

            // 1. Locate seeds.json
            var baseDir = AppContext.BaseDirectory;
            // Adjustment for Docker vs Local run paths might be needed if files are not copied to output.
            // But usually we can embed them or copy them. 
            // For now let's assume valid relative path from execution context or a fixed location.
            // A safer way in .NET is to look relative to the assembly location or ContentRoot.
            
            // NOTE: In a real app, ensure 'Seed' folder is copied to output directory!
            // services/notification-service/Notification.Infrastructure/Seed/seeds.json
            
            // Let's try to find the file dynamically or assume a path.
            // In Docker/Production, ContentRootPath is safer. 
            // Here we will try to read from where the key files are expected.
            
            string seedFilePath = Path.Combine(baseDir, "Seed", "seeds.json");
            
            // Eğer BaseDirectory içinde yoksa (yani build output'a kopyalanmamışsa), kaynak koda gitmeye çalışalım (Development only)
            if (!File.Exists(seedFilePath))
            {
               // Fallback for local development if not copied
               // This requires knowing the relative path to source code which is fragile but works for dev.
               var currentDir = Directory.GetCurrentDirectory();
               // We are likely in Notification.API. 
               // Path to Infrastructure: ../Notification.Infrastructure/Seed/seeds.json
               seedFilePath = Path.GetFullPath(Path.Combine(currentDir, "../Notification.Infrastructure/Seed/seeds.json"));
            }

            if (!File.Exists(seedFilePath))
            {
                logger.LogWarning($"⚠️ Seed file not found at: {seedFilePath}. Skipping seeding.");
                return;
            }

            var jsonContent = await File.ReadAllTextAsync(seedFilePath);
            var seedData = JsonSerializer.Deserialize<List<SeedItem>>(jsonContent);

            if (seedData == null) return;

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
                    continue;
                }

                // Upsert logic
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
                    // Update content if changed (Optional)
                    existingTemplate.Subject = item.Subject;
                    existingTemplate.Body = htmlContent;
                    existingTemplate.UpdatedAt = DateTime.UtcNow;
                    // existingTemplate.Id = item.Id; // Don't change ID
                }
            }

            await context.SaveChangesAsync();
            logger.LogInformation("✅ Notification Templates seeded.");

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error seeding notification templates.");
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
