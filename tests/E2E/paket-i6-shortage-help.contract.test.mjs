// Paket I6 (user decision 14.11.2026): "Saat ekle" opens the hours with the needed time chosen, and "Önemli konulara odaklan"
// says what it would leave out before it is chosen.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const pages = 'clients/admin-panel/src/app/features/coaching-portal/pages/';

test('"Saat ekle" carries the amount to the hours step, which never passes the day limit and saves nothing by itself', async () => {
  const summary = await read(pages + 'wizard-summary-step.component.html');
  assert.match(summary, /addHours\.emit\(weekdayExtra\(view\.shortageMinutes\)\)/);
  assert.match(summary, /addHours\.emit\(weekdayExtra\(lesson\.extraWeeklyMinutes\)\)/);
  assert.doesNotMatch(summary, /addHours\.emit\(\)/);
  const wizard = await read(pages + 'student-study-plan-assistant.component.html');
  assert.match(wizard, /\(addHours\)="addHours\(\$event\)"/);
  assert.match(wizard, /\[addWeekdayMinutes\]="extraWeekday\(\)"/);
  const hours = await read(pages + 'wizard-hours-step.component.ts');
  assert.match(hours, /const to = Math\.min\(this\.limit, from \+ extra\);/);
  assert.doesNotMatch(hours.slice(hours.indexOf('private applyExtra()'), hours.indexOf('addedText()')), /routines\.save|\.save\(/);
});

test('the focus estimate is asked only while a course does not finish and never blocks the page', async () => {
  const summary = await read(pages + 'wizard-summary-step.component.ts');
  assert.match(summary, /if \(!this\.anyLate\(\) \|\| this\.choices\.prioritizeWhenShort\(\)\) return;/);
  assert.match(summary, /this\.engine\.focusEstimate\(request, student\)/);
  assert.match(summary, /if \(run !== this\.estimateRun\) return;/);
  const service = await read('clients/admin-panel/src/app/core/services/coaching-weekly-outcome-plan.service.ts');
  assert.match(service, /focusEstimate\(request:WeeklyOutcomeRequest,studentId\?:string\)\{return this\.preview\(\{\.\.\.request,prioritizeWhenShort:true\},studentId\);\}/);
});
