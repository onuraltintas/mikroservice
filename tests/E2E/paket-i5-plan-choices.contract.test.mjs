// Paket I5 (user decisions 14.11.2026): spaced reviews are the student's choice, a topic's short pieces are one task, and a
// shortage comes with ready ways to close it.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const service = 'services/coaching-service/';
const pages = 'clients/admin-panel/src/app/features/coaching-portal/pages/';

test('spaced reviews are planned only on the days the student chose; nothing is chosen for her', async () => {
  const engine = await read(service + 'Coaching.Application/StudyPlanning/WeeklyPlanEngine.cs');
  assert.match(engine, /public static IReadOnlyList<int> DefaultSpacedDays \{ get; set; \} = \[\];/);
  assert.match(await read(service + 'Coaching.Domain/Entities/StudyPlanPreferences.cs'), /SpacedReviewDayChoices = \[7, 30, 45\];/);
  const plans = await read(service + 'Coaching.Infrastructure/StudyPlanning/CoachingWeeklyOutcomePlanService.cs');
  assert.match(plans, /WeeklyPlanEngine\.RulesFor\(await CoachingStudyPlanPreferencesService\.SpacedDaysAsync\(db,student,ct\)\)/);
  assert.doesNotMatch(plans, /WeeklyPlanEngine\.Rules\b(?!For)/);
  const proposal = await read(service + 'Coaching.Infrastructure/StudyPlanning/CoachingWeeklyProposalService.cs');
  assert.match(proposal, /WeeklyNeed\(Work\[x\.LessonId\]\.Work,Work\[x\.LessonId\]\.Topics,days,times,SpacedDays\)/);
  const migration = await read(service + 'Coaching.Infrastructure/Data/Migrations/20261123090000_SpacedReviewChoice.cs');
  assert.match(migration, /name: "SpacedReviewDays"[\s\S]*table: "study_plan_preferences"[\s\S]*nullable: true/);
  assert.match(await read(service + 'Coaching.API/Controllers/StudyPlanPreferencesController.cs'), /\[HttpPut\("spaced-reviews"\)\]/);
  const wizard = await read(pages + 'student-study-plan-assistant.component.html');
  assert.equal(wizard.match(/\*ngTemplateOutlet="reviewChoice"/g).length, 2);
  assert.match(wizard, /Bitirdiğin konuları sonradan kısa tekrarlarla hatırlatalım mı\?/);
  assert.doesNotMatch(await read(pages + 'wizard-summary-step.component.html'), /bir hafta sonra 10 dakikalık/);
});

test('back-to-back outcomes of one topic are created as one task of at most an hour', async () => {
  assert.match(await read(service + 'Coaching.Application/StudyPlanning/WeeklyPlanEngine.cs'), /DefaultTopicSessionMinutes = 60;/);
  const plans = await read(service + 'Coaching.Infrastructure/StudyPlanning/CoachingWeeklyOutcomePlanService.cs');
  assert.match(plans, /foreach\(var session in WeeklyOutcomeScheduler\.Sessions\(preview\.Schedule\.Tasks,v2\?WeeklyPlanEngine\.TopicSessionMinutes:0\)\)/);
  assert.match(plans, /foreach\(var part in session\)\s*\{\s*var link=StudyPlanTaskOutcome\.Create\(task\.Id,part\.Outcome\.OutcomeId,part\.Outcome\.EstimateId,part\.Minutes,"Catalog"\)/);
});

test('a shortage says what is short, by how much, and offers ready ways to close it', async () => {
  const summary = await read(pages + 'wizard-summary-step.component.html');
  for (const id of ['lesson-gap', 'shortage-solutions', 'late-detail', 'review-cost', 'change-reviews'])
    assert.match(summary, new RegExp(`data-testid="${id}"`));
  assert.match(await read(pages + 'wizard-summary-step.component.ts'), /hafta sonu toplam \$\{weekend\} saat eklersen bütün derslerine yetişirsin/);
});
