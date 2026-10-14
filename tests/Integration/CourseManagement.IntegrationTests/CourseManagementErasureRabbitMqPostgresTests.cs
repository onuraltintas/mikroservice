using CourseManagement.Application.CourseAssignments;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using CourseManagement.Infrastructure.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shared.IntegrationTests.Fixtures;
using Testcontainers.RabbitMq;

namespace CourseManagement.IntegrationTests;

public sealed class CourseManagementErasureRabbitMqPostgresTests
{
    [Fact]
    public async Task Erasure_consumers_persist_assessment_and_execution_and_publish_completion_events()
    {
        var postgres = new PostgresFixture();
        var rabbitMq = new RabbitMqBuilder("rabbitmq:4.3.4-management-alpine")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();
        await postgres.InitializeAsync();

        try
        {
            await rabbitMq.StartAsync();
            var connectionString = postgres.ConnectionString;
            var options = new DbContextOptionsBuilder<CourseManagementDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using (var migrationContext = new CourseManagementDbContext(options))
                await migrationContext.Database.MigrateAsync();

            var services = new ServiceCollection();
            services.AddSingleton<FailOnceExecutionSaveInterceptor>();
            services.AddDbContext<CourseManagementDbContext>((provider, dbOptions) => dbOptions
                .UseNpgsql(connectionString)
                .AddInterceptors(provider.GetRequiredService<FailOnceExecutionSaveInterceptor>()));
            services.AddSingleton<ICourseAssignmentAttachmentStorage, NoOpAttachmentStorage>();
            services.AddSingleton<ICourseManagementLegalHoldStatusProvider>(
                new FixedLegalHoldStatusProvider(CourseManagementLegalHoldStatus.NoActiveHold));
            services.AddSingleton<ErasureCompletionProbe>();
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<CourseManagementAccountErasureService>();
            services.AddScoped<ICourseManagementAccountErasureService>(provider =>
                provider.GetRequiredService<CourseManagementAccountErasureService>());
            services.AddMassTransitTestHarness(configurator =>
            {
                configurator.SetTestTimeouts(testTimeout: TimeSpan.FromSeconds(45));
                configurator.AddConsumer<CourseManagementErasureAssessmentRequestedConsumer>();
                configurator.AddConsumer<CourseManagementErasureExecutionRequestedConsumer>();
                configurator.AddConsumer<AssessmentCompletionProbeConsumer>();
                configurator.AddConsumer<ExecutionCompletionProbeConsumer>();
                configurator.AddEntityFrameworkOutbox<CourseManagementDbContext>(outbox =>
                {
                    outbox.UsePostgres();
                    outbox.UseBusOutbox();
                    outbox.QueryDelay = TimeSpan.FromMilliseconds(100);
                });
                configurator.AddConfigureEndpointsCallback((context, _, endpoint) =>
                {
                    endpoint.UseMessageRetry(retry => retry.Exponential(
                        5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
                    endpoint.UseEntityFrameworkOutbox<CourseManagementDbContext>(context);
                });
                configurator.UsingRabbitMq((context, bus) =>
                {
                    bus.Host(new Uri(rabbitMq.GetConnectionString()));
                    bus.ConfigureEndpoints(context);
                });
            });

            await using var provider = services.BuildServiceProvider(validateScopes: true);
            var harness = provider.GetRequiredService<ITestHarness>();
            var completionProbe = provider.GetRequiredService<ErasureCompletionProbe>();
            var saveFailure = provider.GetRequiredService<FailOnceExecutionSaveInterceptor>();
            await harness.Start();
            try
            {
                var requestId = Guid.NewGuid();
                var subjectUserId = Guid.NewGuid();
                await harness.Bus.Publish(new PersonalDataErasureAssessmentRequestedV1(
                    Guid.NewGuid(), requestId, subjectUserId, DateTime.UtcNow,
                    DryRun: true, Scope: PersonalDataScope.Account));

                (await harness.Consumed.Any<PersonalDataErasureAssessmentRequestedV1>()).Should().BeTrue();
                harness.Published.Select<Fault<PersonalDataErasureAssessmentRequestedV1>>()
                    .Should().BeEmpty();
                var assessmentCompletion = await completionProbe.Assessment.Task.WaitAsync(TimeSpan.FromSeconds(10));
                assessmentCompletion.EventId.Should().NotBe(requestId);
                assessmentCompletion.RequestId.Should().Be(requestId);
                assessmentCompletion.SubjectUserId.Should().Be(subjectUserId);
                assessmentCompletion.ServiceName.Should().Be("CourseManagement");
                assessmentCompletion.RecordCounts.Should().NotBeNull();
                assessmentCompletion.RecordCounts!.Values.Sum().Should().Be(0);

                saveFailure.FailNextExecutionSave();
                await harness.Bus.Publish(new PersonalDataErasureExecutionRequestedV1(
                    Guid.NewGuid(), requestId, subjectUserId, DateTime.UtcNow, PersonalDataScope.Account));

                var executionCompletion = await completionProbe.Execution.Task.WaitAsync(TimeSpan.FromSeconds(10));
                executionCompletion.EventId.Should().NotBe(requestId);
                executionCompletion.RequestId.Should().Be(requestId);
                executionCompletion.ServiceName.Should().Be("CourseManagement");
                executionCompletion.DeletedRecordCount.Should().Be(0);
                saveFailure.ExecutionSaveFailures.Should().Be(1);

                await using var verification = new CourseManagementDbContext(options);
                (await verification.ErasureAssessments.CountAsync()).Should().Be(0);
                (await verification.ErasureExecutions.CountAsync(item => item.RequestId == requestId)).Should().Be(1);
            }
            finally
            {
                await harness.Stop();
            }
        }
        finally
        {
            await rabbitMq.DisposeAsync();
            await postgres.DisposeAsync();
        }
    }

    private sealed class NoOpAttachmentStorage : ICourseAssignmentAttachmentStorage
    {
        public Task<StoredCourseAssignmentAttachment> StoreAsync(string storageKey, Stream content, string contentType,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ErasureCompletionProbe
    {
        public TaskCompletionSource<PersonalDataErasureAssessmentCompletedV1> Assessment { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<PersonalDataErasureExecutionCompletedV1> Execution { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class FailOnceExecutionSaveInterceptor : SaveChangesInterceptor
    {
        private int _failNextExecutionSave;
        private int _executionSaveFailures;

        public int ExecutionSaveFailures => Volatile.Read(ref _executionSaveFailures);

        public void FailNextExecutionSave() => Interlocked.Exchange(ref _failNextExecutionSave, 1);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var hasNewExecution = eventData.Context?.ChangeTracker.Entries<CourseManagementErasureExecution>()
                .Any(entry => entry.State == EntityState.Added) == true;
            if (hasNewExecution && Interlocked.Exchange(ref _failNextExecutionSave, 0) == 1)
            {
                Interlocked.Increment(ref _executionSaveFailures);
                throw new TransientErasurePersistenceException();
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class TransientErasurePersistenceException : Exception { }

    private sealed class AssessmentCompletionProbeConsumer(ErasureCompletionProbe probe)
        : IConsumer<PersonalDataErasureAssessmentCompletedV1>
    {
        public Task Consume(ConsumeContext<PersonalDataErasureAssessmentCompletedV1> context)
        {
            probe.Assessment.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }

    private sealed class ExecutionCompletionProbeConsumer(ErasureCompletionProbe probe)
        : IConsumer<PersonalDataErasureExecutionCompletedV1>
    {
        public Task Consume(ConsumeContext<PersonalDataErasureExecutionCompletedV1> context)
        {
            probe.Execution.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedLegalHoldStatusProvider(CourseManagementLegalHoldStatus status)
        : ICourseManagementLegalHoldStatusProvider
    {
        public Task<CourseManagementLegalHoldStatus> GetStatusAsync(Guid requestId, Guid subjectUserId, CancellationToken cancellationToken) =>
            Task.FromResult(status);
    }
}
