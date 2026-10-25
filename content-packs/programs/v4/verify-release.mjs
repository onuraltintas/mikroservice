import fs from 'node:fs';
import assert from 'node:assert/strict';
import { reviseAssignedSchedule } from './rules.mjs';

const [beforeFile, afterFile] = process.argv.slice(2);
if (!beforeFile || !afterFile) throw new Error('Usage: verify-release.mjs <before.json> <after.json>');
const before = JSON.parse(fs.readFileSync(beforeFile, 'utf8'));
const after = JSON.parse(fs.readFileSync(afterFile, 'utf8'));
const plans = JSON.parse(fs.readFileSync(new URL('./programs.json', import.meta.url), 'utf8'));
assert.equal(after.templates.length, plans.length);
for (const plan of plans) {
  const actual = after.templates.find(item => item.id === plan.id);
  assert.ok(actual);
  assert.deepEqual(actual.pattern, plan.pattern);
}
assert.equal(after.progress.length, before.progress.length);
for (const progress of before.progress) {
  const actual = after.progress.find(item => item.id === progress.id);
  assert.ok(actual);
  const plan = plans.find(item => item.id === progress.templateId);
  assert.deepEqual(actual.schedule, reviseAssignedSchedule(progress, plan, before.exercises));
  for (const field of ['templateId', 'currentWeek', 'currentDay', 'daysCompleted', 'exercisesCompleted', 'logsFingerprint', 'sessionsFingerprint', 'protectedDays']) {
    assert.deepEqual(actual[field], progress[field], `Changed protected progress field: ${field}`);
  }
}
console.log('PASS: live templates and frozen schedules exactly match expected revision; started days/results preserved');
