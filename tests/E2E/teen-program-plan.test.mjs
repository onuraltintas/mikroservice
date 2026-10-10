import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const load = () => JSON.parse(readFileSync(new URL('../../infrastructure/data/teen-program-plan-v2.json', import.meta.url), 'utf8'));
const catalogue = JSON.parse(readFileSync(new URL('../../content-packs/teen-exercises/v1/catalog.json', import.meta.url), 'utf8'));
const visual = new Set(['Focus', 'SchulteTable', 'EyeTracking', 'Saccade', 'Fixation', 'Tachistoscope', 'VisualExpansion']);
const available = {
  Chunking: [1, 2, 3, 4], Comprehension: [1, 2, 3, 4, 5],
  ErrorAnalysis: [4, 5], ExamSimulation: [5], FreeReading: [1, 2, 3],
  RSVP: [3, 4, 5], Skimming: [3, 4, 5], TextFading: [3, 4, 5],
  EyeTracking: [1, 2, 3, 4, 5], Fixation: [1, 2, 3, 4, 5],
  Focus: [1, 2, 3, 4, 5], Saccade: [1, 2, 3, 4, 5],
  Scanning: [1, 2, 3, 4, 5], SchulteTable: [1, 2, 3, 4, 5],
  SpeedReading: [1, 2, 3, 4, 5], Tachistoscope: [1, 2, 3, 4, 5],
  VisualExpansion: [1, 2, 3, 4, 5], Vocabulary: [1, 2, 3, 4, 5],
};

test('teen pack contains exactly five ordered levels for the existing teen age group', () => {
  const pack = load();
  assert.equal(pack.ageGroupId, '10000000-0000-0000-0000-000000000002');
  assert.equal(pack.version, 2);
  assert.deepEqual(pack.programs.map(program => program.level), [1, 2, 3, 4, 5]);
});

for (let level = 1; level <= 5; level++) {
  test(`teen level ${level} has 28 days / 182 tasks, approved balance and available difficulty`, () => {
    const program = load().programs.find(program => program.level === level);
    assert.equal(program.days.length, 28);
    assert.equal(program.days.flat().length, 182);
    program.days.forEach((day, index) => {
      assert.equal(day.length, index < 14 ? 6 : 7);
      assert.equal(day.filter(type => visual.has(type)).length, index < 14 ? 4 : index < 21 ? 3 : 2);
      for (const type of day) {
        const difficulty = type === 'Chunking' ? Math.min(level, 4) : level;
        assert.ok(available[type]?.includes(difficulty), `Missing ${type} at level ${difficulty}`);
        assert.ok(catalogue.some(exercise => exercise.type === type && exercise.difficultyLevel === difficulty
          && exercise.targetAgeGroupId === load().ageGroupId), `Missing actual catalogue entry ${type}/${difficulty}`);
      }
    });
    assert.ok(program.days.some(day => new Set(day).size < day.length));
    assert.ok(program.days.every(day => !day.includes('SubvocalizationReduction') && !day.includes('RegressionReduction')));
  });
}

test('advanced types replace reading tasks only in the final fortnight at supported levels', () => {
  const { programs } = load();
  assert.deepEqual(programs[0].days, programs[1].days);
  for (const program of programs) {
    assert.ok(program.days.every((day, index) => index >= 14 || day.every(type => !['RSVP', 'TextFading', 'Skimming', 'ErrorAnalysis', 'ExamSimulation'].includes(type))));
    if (program.level >= 3) {
      assert.equal(program.days[14][3], 'RSVP');
      assert.equal(program.days[18][2], 'TextFading');
      assert.equal(program.days[21][5], 'Skimming');
    }
    if (program.level >= 4) {
      assert.ok(program.days.every(day => !day.includes('FreeReading')));
      assert.equal(program.days[20][5], 'ErrorAnalysis');
      assert.equal(program.days[26][6], 'ErrorAnalysis');
    }
    if (program.level === 5) assert.equal(program.days[27][5], 'ExamSimulation');
    else assert.ok(program.days.every(day => !day.includes('ExamSimulation')));
  }
});
