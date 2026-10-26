using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CourseManagement.API;
using Microsoft.AspNetCore.Http;

namespace CourseManagement.IntegrationTests;

public sealed class CourseManagementOperationMetricsTests
{
    [Fact]
    public async Task Middleware_RecordsOnlyCourseManagementOperationFailures()
    {
        var measurements = new ConcurrentBag<(string Name, long Value, int StatusCode, string[] TagNames)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CourseManagementOperationMetrics.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var values = tags.ToArray();
            var statusCode = values.Single(tag => tag.Key == "http.response.status_code").Value;
            measurements.Add((instrument.Name, value, (int)statusCode!, values.Select(tag => tag.Key).ToArray()));
        });
        listener.Start();

        await InvokeAsync("/api/course-management/v1/workspaces/00000000-0000-0000-0000-000000000001/lessons", 409);
        await InvokeAsync("/api/course-management/v1/workspaces/00000000-0000-0000-0000-000000000001/finance/payments", 500);
        await InvokeAsync("/api/course-management/v1/workspaces/00000000-0000-0000-0000-000000000001/finance/payments", 500, HttpMethods.Get);
        await InvokeAsync("/api/course-management/v1/workspaces/00000000-0000-0000-0000-000000000001/imports/students", 400);
        await InvokeAsync("/api/course-management/v1/workspaces/00000000-0000-0000-0000-000000000001/courses", 409);

        Assert.Contains(measurements, item => item.Name == CourseManagementOperationMetrics.LessonConflictRejectionsName
            && item.Value == 1 && item.StatusCode == 409);
        Assert.Contains(measurements, item => item.Name == CourseManagementOperationMetrics.PaymentErrorsName
            && item.Value == 1 && item.StatusCode == 500);
        Assert.Contains(measurements, item => item.Name == CourseManagementOperationMetrics.ImportErrorsName
            && item.Value == 1 && item.StatusCode == 400);
        Assert.Equal(3, measurements.Count);
        Assert.All(measurements, item => Assert.Equal("http.response.status_code", Assert.Single(item.TagNames)));

        static async Task InvokeAsync(string path, int statusCode, string? method = null)
        {
            var middleware = new CourseManagementOperationMetricsMiddleware(context =>
            {
                context.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            });
            var context = new DefaultHttpContext();
            context.Request.Method = method ?? HttpMethods.Post;
            context.Request.Path = path;
            await middleware.InvokeAsync(context);
        }
    }
}
