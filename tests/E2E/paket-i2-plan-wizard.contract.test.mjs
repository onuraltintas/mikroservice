// Paket I2 (live user test 13.11.2026, user decisions of the same day): the study plan's load and the wizard.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');

test('one 10-minute review a week after a topic, and the plan asks for 75 % of a catalog duration', async () => {
  const engine = await read('services/coaching-service/Coaching.Application/StudyPlanning/WeeklyPlanEngine.cs');
  assert.match(engine, /public const int SpacedMinutes = 10;/);
  assert.match(engine, /SpacedIntervalDays = \[7\];/);
  assert.match(engine, /public const decimal DefaultContentFactor = \.75m;/);
  const loader = await read('services/coaching-service/Coaching.Infrastructure/StudyPlanning/CoachingWeeklyOutcomePlanService.cs');
  assert.match(loader, /var total=WeeklyPlanEngine\.PlanMinutes\(catalogTotal\);/);
  // The test suites pin the share to 1; production code never sets it.
  for (const path of ['tests/Integration/Coaching.Curriculum.IntegrationTests/TestAssemblySetup.cs', 'tests/Integration/Identity.API.IntegrationTests/TestAssemblySetup.cs'])
    assert.match(await read(path), /WeeklyPlanEngine\.ContentFactor = 1m;/);
  for (const path of ['clients/admin-panel/src/app/features/coaching-portal/pages/wizard-summary-step.component.html',
    'clients/admin-panel/src/app/features/coaching-portal/pages/student-weekly-outcome-plan.component.html']) {
    const page = await read(path);
    assert.doesNotMatch(page, /1, 7 ve 30/);
    // Paket I5: the review days are the student's choice, so no page promises a review after a week.
    assert.doesNotMatch(page, /bir hafta sonra 10 dakikalık kısa bir tekrarla/);
  }
});

test('the weekly proposal places no break; free half hours become 30-minute cards (user decision 17.11.2026)', async () => {
  const engine = await read('services/coaching-service/Coaching.Application/StudyPlanning/WeeklyProposalEngine.cs');
  assert.doesNotMatch(engine, /SessionsBeforeBreak/);
  assert.match(engine, /public const int HalfSessionMinutes = 30;/);
});

test('a disabled publish button says why', async () => {
  const page = await read('clients/admin-panel/src/app/features/coaching-portal/pages/wizard-summary-step.component.html');
  assert.match(page, /data-testid="publish-blocked"/);
  const code = await read('clients/admin-panel/src/app/features/coaching-portal/pages/wizard-summary-step.component.ts');
  assert.match(code, /publishBlockedReason\(\)/);
  assert.match(code, /Önemli konulara odaklan/);
});
