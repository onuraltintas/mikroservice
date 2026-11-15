import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

// User decision 15.11.2026: a study plan runs to the exam (default), through this week, or up to a chosen date. A shorter
// plan is continued with one click, with a choice about untouched past work, and its maker is reminded before it ends.
const read = path => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');
const coaching = 'services/coaching-service/';
const pages = 'clients/admin-panel/src/app/features/coaching-portal/pages/';

test('a plan for part of the way can be created although the lessons do not finish, and remembers it', () => {
  const contract = read(coaching + 'Coaching.Application/StudyPlanning/IWeeklyOutcomePlanService.cs');
  assert.match(contract, /JsonIgnoreCondition\.WhenWritingDefault\)\] bool Partial=false,/);
  assert.match(contract, /JsonIgnoreCondition\.WhenWritingDefault\)\] bool LeaveUnfinished=false\);/);
  const service = read(coaching + 'Coaching.Infrastructure/StudyPlanning/CoachingWeeklyOutcomePlanService.cs');
  assert.match(service, /v2&&request\.PrioritizeWhenShort\|\|request\.Partial;/);
  assert.match(service, /draft\.TimeZoneId,request\.Partial\)/);
  assert.match(service, /if\(request\.LeaveUnfinished&&active is not null\)[\s\S]*?x\.PlannedDate<request\.StartDate[\s\S]*?!left\.Contains\(x\.OutcomeId\)/);
  assert.match(read(coaching + 'Coaching.Domain/Entities/StudyPlan.cs'), /string TimeZoneId=CoachingTime\.ZoneId,[\s\S]{0,200}?bool Partial=false\);/);
});

test('whoever made a plan for part of the way is reminded once when it ends', () => {
  const processor = read(coaching + 'Coaching.Infrastructure/Reminders/CoachingReminderProcessor.cs');
  assert.match(processor, /WeeklySource is \{ Partial: true \} source && source\.EndDate >= today && source\.EndDate <= today\.AddDays\(1\)/);
  assert.match(processor, /plan\.CreatedByTeacherId \?\? plan\.StudentId, plan\.Id, "PlanEnding"/);
  const migration = read(coaching + 'Coaching.Infrastructure/Data/Migrations/20261125090000_PlanEndingReminder.cs');
  assert.match(migration, /'AssignmentDue', 'PlanEnding'\)/);
  assert.match(migration, /DELETE FROM coaching\.reminder_receipts WHERE \\"Kind\\" = 'PlanEnding'/);
  const consumer = read('services/notification-service/Notification.Application/Consumers/CoachingReminderDueConsumer.cs');
  assert.match(consumer, /reminder\.Kind == "PlanEnding"[\s\S]*?"StudyPlanEndingReminder"/);
  assert.match(read('services/notification-service/Notification.Application/Services/NotificationCategories.cs'), /"StudyPlanOverdueReminder", "StudyPlanEndingReminder"\]/);
});

test('the wizard asks how far the plan runs and the plan page continues a short plan', () => {
  const html = read(pages + 'wizard-summary-step.component.html');
  for (const label of ['Plan ne kadarını kapsasın?', 'Sınava kadar', 'Yalnız bu hafta', 'Tarih seç']) assert.match(html, new RegExp(label.replace('?', '\\?')));
  const step = read(pages + 'wizard-summary-step.component.ts');
  assert.match(step, /prioritizeWhenShort: !partial && this\.choices\.prioritizeWhenShort\(\)/);
  assert.match(step, /Yayımlarsan bu plan, yayındaki planın yerini alır/);
  const extend = read(pages + 'plan-extend.component.ts');
  assert.match(extend, /Gelecek haftayı ekle/);
  assert.match(extend, /Önce onlar yerleşsin, yeni konular kalan boşluğa girsin/);
  assert.match(extend, /Bu sefer plana eklenmesin/);
  assert.equal(read(pages + 'student-study-plans.component.html').match(/<app-plan-extend /g)?.length, 2);
});

test('a teacher is told whose plan is ending, and the report says when its figures count a short plan only', () => {
  const processor = read(coaching + 'Coaching.Infrastructure/Reminders/CoachingReminderProcessor.cs');
  assert.match(processor, /ICoachingAccountDirectory\? accounts = null\)/);
  assert.match(processor, /plan\.CreatedByTeacherId\.HasValue && names\.TryGetValue\(plan\.StudentId, out var name\)/);
  assert.match(read('services/notification-service/Notification.Application/Consumers/CoachingReminderDueConsumer.cs'), /için hazırladığın çalışma planı/);
  assert.match(read(coaching + 'Coaching.Infrastructure/StudyPlanning/StudentPlanningPermission.cs'), /source is \{ Partial: true \} \? source\.EndDate : null/);
  assert.match(read(pages + 'student-study-report.component.ts'), /data-testid="partial-plan-note"/);
});
