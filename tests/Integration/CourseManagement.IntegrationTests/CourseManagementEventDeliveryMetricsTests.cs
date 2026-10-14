using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;

namespace CourseManagement.IntegrationTests;

public sealed class CourseManagementEventDeliveryMetricsTests
{
    [Fact]
    public void RecordDeliveryDelay_EmitsElapsedMilliseconds()
    {
        var measurements = new ConcurrentBag<(double Value, int TagCount)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CourseManagementEventDeliveryMetrics.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, value, tags, _) => measurements.Add((value, tags.Length)));
        listener.Start();

        var sentAt = DateTimeOffset.Parse("2026-10-13T09:00:00Z");
        CourseManagementEventDeliveryMetrics.RecordDeliveryDelay(sentAt, sentAt.AddMilliseconds(250));

        Assert.Contains(measurements, measurement => measurement.Value == 250d && measurement.TagCount == 0);
    }

    [Fact]
    public async Task LessonScheduledConsumer_RecordsDeliveryDelayAfterDispatch()
    {
        var measurements = new ConcurrentBag<double>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CourseManagementEventDeliveryMetrics.MeterName
                    && instrument.Name == CourseManagementEventDeliveryMetrics.DeliveryDelayName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        var services = new ServiceCollection();
        services.AddSingleton<ICourseManagementNotificationDispatcher, NoOpDispatcher>();
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.AddConsumer<CourseManagementLessonScheduledConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new CourseManagementLessonScheduledV1(
                Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()], DateTimeOffset.UtcNow.AddDays(1)));

            Assert.True(await harness.Consumed.Any<CourseManagementLessonScheduledV1>());
            Assert.Contains(measurements, delay => delay >= 0);
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class NoOpDispatcher : ICourseManagementNotificationDispatcher
    {
        public Task SendAsync(Guid eventMessageId, IReadOnlyCollection<Guid> recipientIds, string title,
            string message, string type, string? relatedEntityId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
