using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.API.Security;

public sealed class ExercisePreviewMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, OwnedSpeedReadingDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && ExercisePreviewRules.IsPreviewOnlyRole(
                new[] { "Admin", "SystemAdmin", "Editor" }.Where(context.User.IsInRole))
            && ExercisePreviewRules.BlocksPersistentResult(context.Request.Path.Value ?? string.Empty, context.Request.Method)
            && !await HasActiveStaffTrainingAsync(context, db))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "ExercisePreviewOnly",
                message = "Yönetici ve editör egzersizleri yalnızca önizleme modunda tamamlayabilir; sonuçlar kaydedilmez."
            }, context.RequestAborted);
            return;
        }

        await next(context);
    }
    private static async Task<bool> HasActiveStaffTrainingAsync(HttpContext context, OwnedSpeedReadingDbContext db)
    {
        if (!StaffTrainingAccess.IsAllowed(context.User)
            || !Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"), out var userId))
            return false;
        return await db.StudentProgramProgresses.AsNoTracking().AnyAsync(
            progress => progress.UserId == userId && progress.IsActive && progress.IsStaffTraining,
            context.RequestAborted);
    }
}
