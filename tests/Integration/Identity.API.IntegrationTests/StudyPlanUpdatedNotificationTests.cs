using System.Collections.Concurrent;
using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;
namespace Identity.API.IntegrationTests;
public sealed class StudyPlanUpdatedNotificationTests
{
 [Fact]
 public async Task PlanChangeGoesOnlyToTheStudentWithThePlanLinkAndOriginalMessageId()
 {
  var recording=new Recorder();var services=new ServiceCollection();services.AddSingleton<ICoachingNotificationDispatcher>(recording);
  services.AddMassTransitTestHarness(c=>{c.SetTestTimeouts(testInactivityTimeout:TimeSpan.FromSeconds(5));c.AddConsumer<StudyPlanUpdatedConsumer>();c.UsingInMemory((context,bus)=>bus.ConfigureEndpoints(context));});
  await using var provider=services.BuildServiceProvider();var harness=provider.GetRequiredService<ITestHarness>();await harness.Start();
  try {var student=Guid.NewGuid();var plan=Guid.NewGuid();var messageId=Guid.NewGuid();await harness.Bus.Publish(new StudyPlanUpdatedEvent(plan,student,"Matematik: 06.01.2027 18:30"),c=>c.MessageId=messageId);
   Assert.True(await harness.Consumed.Any<StudyPlanUpdatedEvent>());for(var i=0;i<50&&recording.Calls.IsEmpty;i++)await Task.Delay(20);
   var call=Assert.Single(recording.Calls);Assert.Equal(messageId,call.Id);Assert.Equal(student,Assert.Single(call.Recipients));Assert.Equal(plan.ToString(),call.Related);Assert.Equal("StudyPlanUpdated",call.Type);Assert.Contains("Matematik",call.Message);Assert.Equal("Çalışma planın güncellendi",call.Title);
  }finally{await harness.Stop();}
 }
 [Fact]
 public void ProductionConsumerUsesItsOwnQueueWithInboxAndRetry()
 {
  var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!Directory.Exists(Path.Combine(dir.FullName,"services","notification-service")))dir=dir.Parent;
  var source=File.ReadAllText(Path.Combine(dir!.FullName,"services","notification-service","Notification.API","Program.cs"));
  var match=System.Text.RegularExpressions.Regex.Match(source,"ReceiveEndpoint\\(\"coaching-study-plan-updated\".*?\\n        \\}\\);",System.Text.RegularExpressions.RegexOptions.Singleline);
  Assert.True(match.Success);Assert.Contains("ConfigureConsumer<StudyPlanUpdatedConsumer>",match.Value);Assert.Contains("UseEntityFrameworkOutbox<NotificationDbContext>",match.Value);Assert.Contains("UseMessageRetry",match.Value);
 }
 private sealed class Recorder:ICoachingNotificationDispatcher {public ConcurrentQueue<(Guid Id,Guid[] Recipients,string Title,string Message,string Type,string Related)> Calls {get;}=new();public Task SendAsync(Guid id,IReadOnlyCollection<Guid> recipients,string title,string message,string type,string related,CancellationToken ct){Calls.Enqueue((id,recipients.ToArray(),title,message,type,related));return Task.CompletedTask;}}
}
