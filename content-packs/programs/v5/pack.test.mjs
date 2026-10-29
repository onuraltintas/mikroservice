import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { reviseDay, reviseAssignedSchedule, stage } from './rules.mjs';

// Decision 29.10.2026: every N-back (Focus) slot becomes a "Hedef Avı" (TargetHunt) slot at the same age, level and
// position. Nothing else in the programs changes.
const source = JSON.parse(fs.readFileSync(new URL('../v4/programs.json', import.meta.url), 'utf8'));
const programs = JSON.parse(fs.readFileSync(new URL('./programs.json', import.meta.url), 'utf8'));
const days = program => Array.from({ length: 28 }, (_, index) => program.pattern[`week${Math.floor(index / 7) + 1}`][`day${index % 7 + 1}`]);

test('all 20 programs keep their ids, levels, 560 days and 3640 slots', () => {
  assert.equal(programs.length, 20);
  assert.deepEqual(programs.map(program => program.id), source.map(program => program.id));
  for (const program of programs) {
    assert.deepEqual(program.previousPattern, source.find(item => item.id === program.id).pattern);
    assert.equal(days(program).flat().reduce((sum, slot) => sum + slot.count, 0), 182);
  }
});

test('every Focus slot becomes TargetHunt in place and nothing else changes', () => {
  let replaced = 0;
  for (const program of programs) {
    const previous = days(source.find(item => item.id === program.id));
    days(program).forEach((day, index) => {
      assert.equal(day.length, previous[index].length);
      day.forEach((slot, position) => {
        const before = previous[index][position];
        if (before.type === 'Focus') { replaced++; assert.deepEqual(slot, { ...before, type: 'TargetHunt' }); }
        else assert.deepEqual(slot, before);
      });
      assert.ok(day.every(slot => slot.type !== 'Focus'));
      assert.deepEqual(day.map(slot => stage(slot.type)), day.map(slot => stage(slot.type)).toSorted());
    });
  }
  assert.ok(replaced >= 560, `expected at least one attention slot per day, got ${replaced}`);
});

test('assigned schedules keep started days and switch other Focus tasks to the same age and level', () => {
  const exercises = [
    { id: 'focus-1', type: 'Focus', ageId: 'adult', level: 1 },
    { id: 'focus-2', type: 'Focus', ageId: 'adult', level: 2 },
    { id: 'hunt-1', type: 'TargetHunt', ageId: 'adult', level: 1 },
    { id: 'hunt-2', type: 'TargetHunt', ageId: 'adult', level: 2 },
    { id: 'eye', type: 'EyeTracking', ageId: 'adult', level: 1 }
  ];
  const schedule = [1, 2, 3].flatMap(day => ['eye', day === 3 ? 'focus-2' : 'focus-1']
    .map((id, index) => ({ WeekNumber: 1, DayNumber: day, Order: index + 1, ExerciseId: id })));
  const progress = { schedule, protectedDays: [1] }, program = { age: 'Adult', ageId: 'adult' };

  const updated = reviseAssignedSchedule(progress, program, exercises);

  assert.deepEqual(updated.slice(0, 2), schedule.slice(0, 2));
  assert.deepEqual(updated.slice(2).map(slot => slot.ExerciseId), ['eye', 'hunt-1', 'eye', 'hunt-2']);
  assert.deepEqual(reviseAssignedSchedule({ ...progress, schedule: updated }, program, exercises), updated);
  assert.throws(() => reviseAssignedSchedule(progress, program, exercises.filter(item => item.id !== 'hunt-2')), /Missing TargetHunt/);
});

test('reviseDay is a pure Focus to TargetHunt rename and rejects unknown types', () => {
  assert.deepEqual(reviseDay([{ type: 'EyeTracking', count: 1, difficulty: 2 }, { type: 'Focus', count: 1, difficulty: 2 }]),
    [{ type: 'EyeTracking', count: 1, difficulty: 2 }, { type: 'TargetHunt', count: 1, difficulty: 2 }]);
  assert.throws(() => reviseDay([{ type: 'Unknown', count: 1, difficulty: 1 }]), /Unclassified/);
});
