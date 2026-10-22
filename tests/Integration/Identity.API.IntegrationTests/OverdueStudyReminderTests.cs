using Coaching.Domain.Entities;
namespace Identity.API.IntegrationTests;
public sealed class OverdueStudyReminderTests
{
 [Fact]
 public void OverdueSummaryHasStableDailyIdentitySeparateFromTodayReminder()
 {
  var student=Guid.NewGuid();var plan=Guid.NewGuid();var now=new DateTime(2026,10,23,6,0,0,DateTimeKind.Utc);
  var overdue=CoachingReminderReceipt.Create(student,plan,"StudyOverdue","2026-10-23",now,now);
  var retry=CoachingReminderReceipt.Create(student,plan,"StudyOverdue","2026-10-23",now,now);
  var today=CoachingReminderReceipt.Create(student,plan,"StudyDay","2026-10-23",now,now);
  Assert.Equal(overdue.Id,retry.Id);Assert.NotEqual(today.Id,overdue.Id);
 }
}
