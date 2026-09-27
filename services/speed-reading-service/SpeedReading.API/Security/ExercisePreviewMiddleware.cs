using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.API.Security;

public sealed class ExercisePreviewMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && ExercisePreviewRules.IsPreviewOnlyRole(
                new[] { "Admin", "SystemAdmin", "Editor" }.Where(context.User.IsInRole))
            && ExercisePreviewRules.BlocksPersistentResult(context.Request.Path.Value ?? string.Empty, context.Request.Method))
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
}
