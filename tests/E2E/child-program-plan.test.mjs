import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

test('child replacement plan has 28 days, 182 tasks and approved weekly balance', () => {
  const plan = JSON.parse(readFileSync(new URL('../../infrastructure/data/child-program-plan-v2.json', import.meta.url), 'utf8'));
  const visual = new Set(['Focus', 'SchulteTable', 'EyeTracking', 'Saccade', 'Fixation', 'Tachistoscope', 'VisualExpansion']);
  assert.equal(plan.days.length, 28);
  assert.equal(plan.days.flat().length, 182);
  plan.days.forEach((day, index) => {
    assert.equal(day.length, index < 14 ? 6 : 7);
    assert.equal(day.filter(type => visual.has(type)).length, index < 14 ? 4 : index < 21 ? 3 : 2);
  });
  assert.ok(plan.days.some(day => new Set(day).size < day.length));
});
