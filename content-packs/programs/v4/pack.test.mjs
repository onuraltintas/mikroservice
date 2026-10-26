import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { reviseDay, reviseAssignedSchedule, stage } from './rules.mjs';

const source = JSON.parse(fs.readFileSync(new URL('../v3/programs.json', import.meta.url), 'utf8'));
const file = new URL('./programs.json', import.meta.url);
const programs = JSON.parse(fs.readFileSync(fs.existsSync(file) ? file : new URL('../v3/programs.json', import.meta.url), 'utf8'));
const days = program => Array.from({ length: 28 }, (_, index) => program.pattern[`week${Math.floor(index / 7) + 1}`][`day${index % 7 + 1}`]);

test('all 20 programs retain IDs, levels, 560 days and 3640 slots', () => {
  assert.equal(programs.length, 20);
  assert.deepEqual(programs.map(program => program.id), source.map(program => program.id));
  for (const program of programs) {
    const schedule = days(program);
    assert.equal(schedule.flat().reduce((sum, slot) => sum + slot.count, 0), 182);
    schedule.forEach((day, index) => {
      assert.equal(day.reduce((sum, slot) => sum + slot.count, 0), index < 14 ? 6 : 7);
      assert.ok(day.every(slot => slot.difficulty === program.level));
    });
  }
});

test('every day starts with visual practice and places all reading and attention afterwards', () => {
  for (const program of programs) days(program).forEach((day, index) => {
    assert.equal(stage(day[0].type), 0, `${program.age} S${program.level} day ${index + 1}`);
    assert.deepEqual(day.map(slot => stage(slot.type)), day.map(slot => stage(slot.type)).toSorted());
  });
});

test('vocabulary stays only in child and teen plans without changing their counts', () => {
  for (const program of programs) {
    const count = days(program).flat().filter(slot => slot.type === 'Vocabulary').reduce((sum, slot) => sum + slot.count, 0);
    assert.equal(count, ['Child', 'Teen'].includes(program.age) ? (program.level < 4 ? 14 : 13) : 0);
  }
});

test('advanced work, comprehension modes and all other types are preserved', () => {
  for (const program of programs) {
    const previous = source.find(item => item.id === program.id);
    for (let index = 0; index < 28; index++) {
      const before = days(previous)[index], after = days(program)[index];
      const changed = ['YoungAdult', 'Adult'].includes(program.age);
      for (const slot of before) {
        if (changed && slot.type === 'Vocabulary') continue;
        assert.ok(after.some(other => JSON.stringify(other) === JSON.stringify(slot)));
      }
      assert.ok(after.every(slot => slot.type !== 'ExamSimulation'));
      assert.deepEqual(after, reviseDay(program.age, after, index + 1));
    }
  }
});

test('frozen schedules preserve started days and select age/level-specific replacements on other days', () => {
  const exercises = [
    { id: 'focus', type: 'Focus', ageId: 'adult', level: 1 },
    { id: 'eye', type: 'EyeTracking', ageId: 'adult', level: 1 },
    { id: 'vocab', type: 'Vocabulary', ageId: 'adult', level: 1 },
    { id: 'reading', type: 'SpeedReading', ageId: 'adult', level: 1 }
  ];
  const schedule = [1, 2].flatMap(day => ['focus', 'eye', 'vocab'].map((id, index) => ({ WeekNumber: 1, DayNumber: day, Order: index + 1, ExerciseId: id })));
  const progress = { schedule, protectedDays: [1] }, program = { age: 'Adult', ageId: 'adult' };
  const updated = reviseAssignedSchedule(progress, program, exercises);
  assert.deepEqual(updated.slice(0, 3), schedule.slice(0, 3));
  assert.deepEqual(updated.slice(3).map(slot => slot.ExerciseId), ['eye', 'reading', 'focus']);
  assert.deepEqual(updated.slice(3).map(slot => slot.Order), [1, 2, 3]);
  assert.deepEqual(schedule, progress.schedule);
  assert.deepEqual(reviseAssignedSchedule({ ...progress, schedule: updated }, program, exercises), updated);
  assert.throws(() => reviseAssignedSchedule(progress, program, exercises.filter(exercise => exercise.id !== 'reading')), /Missing normal replacement/);
});

test('unknown exercise types are not silently placed in a new order', () => {
  assert.throws(() => reviseDay('Child', [{ type: 'Unknown' }], 1), /Unclassified/);
});
