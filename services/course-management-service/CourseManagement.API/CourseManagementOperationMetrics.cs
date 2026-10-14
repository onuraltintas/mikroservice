namespace CourseManagement.API;

public sealed class CourseManagementOperationMetricsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            RecordFailure(context.Request.Method, context.Request.Path, StatusCodes.Status500InternalServerError);
            throw;
        }

        RecordFailure(context.Request.Method, context.Request.Path, context.Response.StatusCode);
    }

    private static void RecordFailure(string method, PathString path, int statusCode)
    {
        if (statusCode < StatusCodes.Status400BadRequest || HttpMethods.IsGet(method))
            return;

        var route = path.Value ?? string.Empty;
        if (statusCode == StatusCodes.Status409Conflict
            && (route.Contains("/lessons", StringComparison.OrdinalIgnoreCase)
                || route.Contains("/lesson-series", StringComparison.OrdinalIgnoreCase)))
            CourseManagementOperationMetrics.RecordLessonConflict(statusCode);

        if (route.Contains("/finance/payments", StringComparison.OrdinalIgnoreCase))
            CourseManagementOperationMetrics.RecordPaymentError(statusCode);

        if (route.Contains("/imports/", StringComparison.OrdinalIgnoreCase))
            CourseManagementOperationMetrics.RecordImportError(statusCode);
    }
}
