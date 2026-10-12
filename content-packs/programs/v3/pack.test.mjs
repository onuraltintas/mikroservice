import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const dir = path.dirname(fileURLToPath(import.meta.url));
const programs = fs.existsSync(path.join(dir, 'programs.json'))
  ? JSON.parse(fs.readFileSync(path.join(dir, 'programs.json'), 'utf8'))
  : ['child', 'teen', 'youngadult', 'adult'].flatMap(age =>
    JSON.parse(fs.readFileSync(path.join(dir, `baseline-${age}.json`), 'utf8')).programs);
const days = p => Array.from({ length: 28 }, (_, i) => p.pattern[`week${Math.floor(i / 7) + 1}`][`day${i % 7 + 1}`]);
const counts = p => days(p).flat().reduce((result, x) => (result[x.type] = (result[x.type] ?? 0) + x.count, result), {});

test('20 training programs preserve 560 days and 3640 independent tasks', () => {
  assert.equal(programs.length, 20);
  for (const p of programs) {
    const schedule = days(p);
    assert.equal(schedule.reduce((n, day) => n + day.reduce((m, x) => m + x.count, 0), 0), 182);
    schedule.forEach((day, i) => assert.equal(day.reduce((n, x) => n + x.count, 0), i < 14 ? 6 : 7));
  }
});

test('exam simulation is excluded; visualization, fading and independent reading are allocated', () => {
  for (const p of programs) {
    const c = counts(p);
    assert.equal(c.ExamSimulation ?? 0, 0, `${p.age} ${p.level}: exam`);
    assert.equal(c.Visualization, 2, `${p.age} ${p.level}: visualization`);
    assert.equal(c.TextFading, p.level < 3 ? 1 : 2, `${p.age} ${p.level}: fading`);
    for (let week = 0; week < 4; week++)
      assert.ok(days(p).slice(week * 7, week * 7 + 7).flat().some(x => x.type === 'FreeReading'));
  }
});

test('exact current levels and explicit comprehension modes are used', () => {
  for (const p of programs) {
    for (const day of days(p)) for (const slot of day) {
      assert.equal(slot.difficulty, p.level, `${p.age} S${p.level} ${slot.type}`);
      if (slot.type === 'Comprehension') assert.ok(['learning', 'evaluation'].includes(slot.comprehensionMode));
    }
    const evaluation = days(p).flat().filter(x => x.type === 'Comprehension' && x.comprehensionMode === 'evaluation');
    assert.equal(evaluation.length, 4);
  }
});

test('advanced allocations obey age and level boundaries without removing foundational work', () => {
  for (const p of programs) {
    const c = counts(p), child = p.age === 'Child';
    assert.equal(c.Skimming ?? 0, p.level >= 3 ? 2 : 0);
    assert.equal(c.RSVP ?? 0, p.level >= (child ? 4 : 3) ? 2 : 0);
    assert.equal(c.ErrorAnalysis ?? 0, p.level >= 4 ? 2 : 0);
    assert.equal(c.RegressionReduction ?? 0, p.level >= (child ? 2 : 1) ? 2 : 0);
    assert.equal(c.SubvocalizationReduction ?? 0, p.level >= (child ? 4 : p.age === 'Teen' ? 3 : 2) ? 2 : 0);
    for (const type of ['Focus', 'EyeTracking', 'Tachistoscope', 'VisualExpansion', 'SchulteTable', 'Saccade', 'Fixation', 'Chunking', 'Scanning', 'SpeedReading', 'Vocabulary'])
      assert.ok(c[type] > 0, `${p.age} S${p.level} ${type}`);
  }
});
