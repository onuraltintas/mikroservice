using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.API.Security;

public sealed class SpeedReadingSubscriptionAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ISpeedReadingSubscription subscriptions,
        OwnedSpeedReadingDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !context.User.IsInRole("Student")
            || (HasStaffRole(context.User) && !IsStudentTrainingRequest(context.Request.Path))
            || !SpeedReadingSubscriptionAccessRules.RequiresSubscription(
                context.Request.Path.Value ?? string.Empty,
                context.Request.Method))
        {
            await next(context);
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (await IsAssessmentSessionAsync(context, db, userId))
        {
            await next(context);
            return;
        }

        var access = await subscriptions.GetMyAccessAsync(userId, context.RequestAborted);
        if (!access.HasSpeedReading)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "SubscriptionRequired",
                message = "Hızlı okuma aboneliğiniz sona erdi. Devam etmek için aboneliğinizi yenileyin."
            }, context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool HasStaffRole(ClaimsPrincipal user) =>
        new[] { "Admin", "SystemAdmin", "Teacher", "Editor", "Coach", "InstitutionAdmin", "InstitutionOwner" }
            .Any(user.IsInRole);

    private static bool IsStudentTrainingRequest(PathString path) =>
        new[]
        {
            "/api/speed-reading/daily-progress",
            "/api/speed-reading/progress/programs",
            "/api/speed-reading/student-program",
            "/api/speed-reading/exercise-sessions",
            "/api/speed-reading/student-reading",
            "/api/speed-reading/reviews",
            "/api/speed-reading/review-exercises",
            "/api/speed-reading/learning-paths/personalized",
            "/api/speed-reading/adaptive-learning/dashboard"
        }.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private static async Task<bool> IsAssessmentSessionAsync(
        HttpContext context,
        OwnedSpeedReadingDbContext db,
        Guid userId)
    {
        var parts = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: >= 4 }
            || !parts[2].Equals("exercise-sessions", StringComparison.OrdinalIgnoreCase))
            return false;

        if (parts[3].Equals("start", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsPost(context.Request.Method))
        {
            context.Request.EnableBuffering();
            try
            {
                using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                var root = document.RootElement;
                return root.ValueKind == JsonValueKind.Object
                    && (root.TryGetProperty("assessmentAttemptId", out var attemptId)
                        || root.TryGetProperty("AssessmentAttemptId", out attemptId))
                    && attemptId.ValueKind == JsonValueKind.String
                    && Guid.TryParse(attemptId.GetString(), out var parsedId)
                    && parsedId != Guid.Empty;
            }
            catch (JsonException)
            {
                return false;
            }
            finally
            {
                context.Request.Body.Position = 0;
            }
        }

        return Guid.TryParse(parts[3], out var sessionId)
            && await db.ExerciseSessions.AsNoTracking().AnyAsync(
                session => session.Id == sessionId
                    && session.StudentId == userId
                    && session.AssessmentAttemptId.HasValue,
                context.RequestAborted);
    }
}
