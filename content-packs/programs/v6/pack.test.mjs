import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { blockOf, buildPattern, levelRange, reviseAssignedSchedule, tasksPerDay, eyeWarmups } from './rules.mjs';

// Decision 30.10.2026 (programs v6): every day follows the same blocks — warm-up (eye or attention), perception and
// chunking, reading, comprehension, vocabulary (Child/Teen only) — while the weekly weight moves from eye warm-ups and
// perception speed (week 1) through chunking (week 2) and chunked fast reading (week 3) to real reading (week 4).
// Child programs keep 6 tasks a day; the other ages have 6 tasks on days 1–14 and 7 on days 15–28.
const source = JSON.parse(fs.readFileSync(new URL('../v5/programs.json', import.meta.url), 'utf8'));
const programs = JSON.parse(fs.readFileSync(new URL('./programs.json', import.meta.url), 'utf8'));
const days = program => Array.from({ length: 28 }, (_, index) => program.pattern[`week${Math.floor(index / 7) + 1}`][`day${index % 7 + 1}`]);
const week = (program, number) => days(program).slice((number - 1) * 7, number * 7);
const kids = age => age === 'Child' || age === 'Teen';

test('keeps the 20 programs, their ids, ages, levels and placement; v5 is the previous pattern', () => {
  assert.equal(programs.length, 20);
  assert.deepEqual(programs.map(program => [program.id, program.age, program.ageId, program.level]),
    source.map(program => [program.id, program.age, program.ageId, program.level]));
  for (const program of programs) {
    const previous = source.find(item => item.id === program.id);
    assert.deepEqual(program.previousPattern, previous.pattern);
    assert.equal(program.pattern.placementLevel, previous.pattern.placementLevel);
  }
});

test('Child has 6 tasks every day (168); other ages 6 then 7 (182)', () => {
  for (const program of programs) {
    days(program).forEach((day, index) => {
      assert.equal(day.reduce((sum, slot) => sum + slot.count, 0), tasksPerDay(program.age, index + 1), `${program.age} day ${index + 1}`);
      assert.ok(day.every(slot => slot.count === 1));
    });
    assert.equal(days(program).flat().length, program.age === 'Child' ? 168 : 182);
  }
});

test('every day runs warm-up → perception/chunking → reading → comprehension → vocabulary', () => {
  for (const program of programs) days(program).forEach((day, index) => {
    const blocks = day.map(slot => blockOf(slot.type));
    assert.equal(blocks[0], 0, `${program.age}/${program.level} day ${index + 1} starts with a warm-up`);
    assert.deepEqual(blocks, blocks.toSorted((left, right) => left - right), `${program.age}/${program.level} day ${index + 1} order`);
    assert.equal(blocks.filter(block => block === 0).length, 1, 'one warm-up a day');
    const comprehension = day.filter(slot => slot.type === 'Comprehension');
    assert.equal(comprehension.length, 1, 'one comprehension text a day');
    assert.equal(comprehension[0].comprehensionMode, (index + 1) % 7 === 0 ? 'evaluation' : 'learning');
    assert.equal(day.filter(slot => slot.type === 'Vocabulary').length, kids(program.age) ? 1 : 0);
    assert.ok(day.every(slot => slot.type !== 'Focus'));
  });
});

test('weekly weight follows the decided sequence', () => {
  for (const program of programs) {
    const count = (number, types) => week(program, number).flat().filter(slot => types.includes(slot.type)).length;
    // Week 1: eye warm-ups and perception speed (tachistoscope + peripheral letters every day), no chunking yet.
    assert.equal(count(1, ['Tachistoscope']), 7);
    assert.equal(count(1, ['VisualExpansion']), 7);
    assert.equal(count(1, ['Chunking']), 0);
    // Weeks 2–4: chunking every day.
    for (const number of [2, 3, 4]) assert.equal(count(number, ['Chunking']), 7, `week ${number} chunking`);
    // Perception practice tapers off.
    const perception = [1, 2, 3, 4].map(number => count(number, ['Tachistoscope', 'VisualExpansion']));
    assert.ok(perception[0] > perception[1] && perception[1] >= perception[2] && perception[2] >= perception[3], perception.join());
    // Eye warm-ups give way to attention warm-ups.
    const eye = [1, 2, 3, 4].map(number => count(number, [...eyeWarmups]));
    assert.ok(eye[0] + eye[1] > eye[2] + eye[3], eye.join());
    assert.ok(count(3, ['TargetHunt', 'SchulteTable']) > count(1, ['TargetHunt', 'SchulteTable']));
    // Reading grows: weeks 3–4 have more reading tasks than weeks 1–2.
    const reading = [1, 2, 3, 4].map(number => week(program, number).flat().filter(slot => blockOf(slot.type) === 2).length);
    assert.ok(reading[2] + reading[3] > reading[0] + reading[1], reading.join());
  }
});

test('slot difficulty is the program level, capped to the levels the catalogue has for that age', () => {
  for (const program of programs) for (const slot of days(program).flat()) {
    const [minimum, maximum] = levelRange(slot.type, program.age);
    assert.ok(program.level >= minimum, `${program.age}/${program.level} uses ${slot.type} below its first level`);
    assert.equal(slot.difficulty, Math.min(program.level, maximum), `${program.age}/${program.level}/${slot.type}`);
  }
});

test('higher levels use more reading tools than level 1', () => {
  for (const age of ['Child', 'Teen', 'YoungAdult', 'Adult']) {
    const types = level => new Set(days(programs.find(item => item.age === age && item.level === level)).flat().map(slot => slot.type));
    assert.ok(types(5).size > types(1).size, age);
    assert.ok(types(5).has('ErrorAnalysis'));
    assert.ok(!types(1).has('ErrorAnalysis'));
  }
});

test('descriptions describe the new structure and task totals', () => {
  for (const program of programs) {
    assert.match(program.description, program.age === 'Child' ? /168 görev/ : /182 görev/);
    assert.match(program.description, /gruplama/i);
    if (kids(program.age)) assert.match(program.description, /kelime/i);
  }
});

test('buildPattern is deterministic', () => {
  assert.deepEqual(buildPattern('Teen', 3), buildPattern('Teen', 3));
});

const catalogue = ageId => ['EyeTracking', 'Saccade', 'Fixation', 'SchulteTable', 'TargetHunt', 'Tachistoscope', 'VisualExpansion',
  'Chunking', 'SpeedReading', 'FreeReading', 'Scanning', 'Skimming', 'RSVP', 'TextFading', 'RegressionReduction',
  'SubvocalizationReduction', 'Visualization', 'ErrorAnalysis', 'Vocabulary', 'Comprehension', 'Focus']
  .flatMap(type => [1, 2, 3, 4, 5].flatMap(level => (type === 'Comprehension' ? ['learning', 'evaluation'] : [null])
    .map(mode => ({ id: `${type}-${level}${mode ? `-${mode}` : ''}`, type, ageId, level, mode }))));

test('active schedules keep protected days and rebuild the other days from v6 at the day\'s own level', () => {
  const program = programs.find(item => item.age === 'Child' && item.level === 3);
  const exercises = catalogue(program.ageId);
  // v5-shaped schedule: day 1 finished, days 2–28 open; the week-2 days were adapted one level up (H7).
  const schedule = [];
  for (let day = 1; day <= 28; day++) {
    const level = day >= 8 && day <= 14 ? 4 : 3;
    const slots = days(source.find(item => item.id === program.id))[day - 1];
    slots.forEach((slot, index) => schedule.push({ WeekNumber: Math.floor((day - 1) / 7) + 1, DayNumber: (day - 1) % 7 + 1,
      Order: index + 1, ExerciseId: exercises.find(item => item.type === slot.type && item.level === level
        && (!slot.comprehensionMode || item.mode === slot.comprehensionMode)).id }));
  }
  const progress = { schedule, protectedDays: [1] };

  const updated = reviseAssignedSchedule(progress, program, exercises);

  const inDay = day => slot => (slot.WeekNumber - 1) * 7 + slot.DayNumber === day;
  assert.deepEqual(updated.filter(inDay(1)), schedule.filter(inDay(1)));
  const byId = new Map(exercises.map(item => [item.id, item]));
  for (let day = 2; day <= 28; day++) {
    const slots = updated.filter(inDay(day));
    const expected = days(program)[day - 1];
    assert.deepEqual(slots.map(slot => slot.Order), expected.map((_, index) => index + 1));
    assert.deepEqual(slots.map(slot => byId.get(slot.ExerciseId).type), expected.map(slot => slot.type), `day ${day} types`);
    const level = day >= 8 && day <= 14 ? 4 : 3;
    for (const [index, slot] of slots.entries()) {
      const exercise = byId.get(slot.ExerciseId);
      assert.equal(exercise.level, Math.min(level, levelRange(exercise.type, program.age)[1]), `day ${day} ${exercise.type}`);
      if (expected[index].comprehensionMode) assert.equal(exercise.mode, expected[index].comprehensionMode);
    }
  }
  assert.equal(updated.length, 6 + 27 * 6);
  assert.deepEqual(reviseAssignedSchedule({ ...progress, schedule: updated }, program, exercises), updated, 'idempotent');
  assert.equal(reviseAssignedSchedule({ schedule: null, protectedDays: [] }, program, exercises), null);
  // An open day-8 schedule without chunking in it, rebuilt against a catalogue that has no chunking exercise.
  const noChunking = exercises.filter(item => item.type !== 'Chunking');
  const day8 = schedule.filter(inDay(1)).map(slot => ({ ...slot, WeekNumber: 2, DayNumber: 1 }));
  assert.throws(() => reviseAssignedSchedule({ schedule: day8, protectedDays: [] }, program, noChunking), /Missing exercise/);
});
