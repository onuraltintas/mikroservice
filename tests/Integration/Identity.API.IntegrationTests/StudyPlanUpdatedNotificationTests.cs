using System.Collections.Concurrent;
using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;
namespace Identity.API.IntegrationTests;
public sealed class StudyPlanUpdatedNotificationTests
{
 [Fact]
 public async Task PlanChangeGoesOnlyToTheStudentWithThePlanLinkAndOriginalMessageId()
 {
  var recording=new Recorder();var services=new ServiceCollection();services.AddSingleton<ICoachingNotificationDispatcher>(recording);
  services.AddMassTransitTestHarness(c=>{c.UseSuiteTimeouts();c.AddConsumer<StudyPlanUpdatedConsumer>();c.UsingInMemory((context,bus)=>bus.ConfigureEndpoints(context));});
  await using var provider=services.BuildServiceProvider();var harness=provider.GetRequiredService<ITestHarness>();await harness.Start();
  try {var student=Guid.NewGuid();var plan=Guid.NewGuid();var messageId=Guid.NewGuid();await harness.Bus.Publish(new StudyPlanUpdatedEvent(plan,student,"Matematik: 06.01.2027 18:30"),c=>c.MessageId=messageId);
   Assert.True(await harness.Consumed.Any<StudyPlanUpdatedEvent>());await TestHarnessTimeouts.UntilAsync(()=>!recording.Calls.IsEmpty);
   var call=Assert.Single(recording.Calls);Assert.Equal(messageId,call.Id);Assert.Equal(student,Assert.Single(call.Recipients));Assert.Equal(plan.ToString(),call.Related);Assert.Equal("StudyPlanUpdated",call.Type);Assert.Contains("Matematik",call.Message);Assert.Equal("Çalışma planın güncellendi",call.Title);
  }finally{await harness.Stop();}
 }
 [Fact]
 public async Task ATeachersChangesToOnePlanWithinHalfAnHourAreOneNotice()
 {
  // Live test 16.11.2026 (#36): a student got a notice for every drag of the teacher.
  var recording=new Recorder();var services=new ServiceCollection();services.AddSingleton<ICoachingNotificationDispatcher>(recording);
  var name=Guid.NewGuid().ToString();NotificationDbContext Open()=>new(new DbContextOptionsBuilder<NotificationDbContext>().UseInMemoryDatabase(name).Options);
  services.AddScoped<INotificationDbContext>(_=>Open());var db=Open();
  services.AddMassTransitTestHarness(c=>{c.UseSuiteTimeouts();c.AddConsumer<StudyPlanUpdatedConsumer>();c.UsingInMemory((context,bus)=>bus.ConfigureEndpoints(context));});
  await using var provider=services.BuildServiceProvider();var harness=provider.GetRequiredService<ITestHarness>();await harness.Start();
  try {var student=Guid.NewGuid();var plan=Guid.NewGuid();
   var earlier=NotificationItem.Create(student,"Çalışma planın güncellendi","Matematik: 06.01.2027 18:30","StudyPlanUpdated",plan.ToString());
   db.Notifications.Add(earlier);await db.SaveChangesAsync(CancellationToken.None);
   await harness.Bus.Publish(new StudyPlanUpdatedEvent(plan,student,"Fen: 07.01.2027 17:00"));
   Assert.True(await harness.Consumed.Any<StudyPlanUpdatedEvent>());
   await TestHarnessTimeouts.UntilAsync(()=>{using var read=Open();return read.Notifications.Single().Message==StudyPlanUpdatedConsumer.MergedMessage;});
   Assert.Empty(recording.Calls);
   using(var read=Open())Assert.Single(read.Notifications);
  }finally{await harness.Stop();}
 }
 [Fact]
 public async Task APlanTheStudentPublishedThemselvesNeedsNoNoticeButTheTeachersDoes()
 {
  // Live test 16.11.2026 (#12): three self-published plans gave three "Çalışma planın hazır" notices.
  var recording=new Recorder();var services=new ServiceCollection();services.AddSingleton<ICoachingNotificationDispatcher>(recording);
  services.AddMassTransitTestHarness(c=>{c.UseSuiteTimeouts();c.AddConsumer<StudyPlanPublishedConsumer>();c.UsingInMemory((context,bus)=>bus.ConfigureEndpoints(context));});
  await using var provider=services.BuildServiceProvider();var harness=provider.GetRequiredService<ITestHarness>();await harness.Start();
  try {var student=Guid.NewGuid();var teacher=Guid.NewGuid();
   await harness.Bus.Publish(new StudyPlanPublishedEvent(Guid.NewGuid(),student){PublishedBy=student});
   await harness.Bus.Publish(new StudyPlanPublishedEvent(Guid.NewGuid(),student){PublishedBy=teacher});
   await TestHarnessTimeouts.UntilAsync(()=>harness.Consumed.Select<StudyPlanPublishedEvent>().Count()==2);
   await TestHarnessTimeouts.UntilAsync(()=>!recording.Calls.IsEmpty);
   var call=Assert.Single(recording.Calls);Assert.Equal("Çalışma planın hazır",call.Title);
  }finally{await harness.Stop();}
 }
 private sealed class Recorder:ICoachingNotificationDispatcher {public ConcurrentQueue<(Guid Id,Guid[] Recipients,string Title,string Message,string Type,string Related)> Calls {get;}=new();public Task SendAsync(Guid id,IReadOnlyCollection<Guid> recipients,string title,string message,string type,string related,CancellationToken ct){Calls.Enqueue((id,recipients.ToArray(),title,message,type,related));return Task.CompletedTask;}}
}
