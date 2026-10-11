import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const ages = {
  child: [[1,3000,32],[1,2800,40],[1,2600,48],[2,3000,48],[2,2800,56]],
  teen: [[1,2800,40],[1,2500,48],[2,2800,48],[2,2500,56],[3,3000,56]],
  'young-adult': [[1,2600,48],[2,2900,48],[2,2600,56],[3,3000,56],[3,2700,64]],
  adult: [[1,2700,48],[2,3000,48],[2,2700,56],[3,3100,56],[3,2800,64]]
};
for (const [age, expected] of Object.entries(ages)) {
  test(`${age}: all five approved N-back loads, cadences and trial counts`, () => {
    const catalog = JSON.parse(readFileSync(new URL(`../../content-packs/${age}-exercises/v1/catalog.json`, import.meta.url)));
    const rows = catalog.filter(row => row.type === 'Focus').sort((a,b) => a.difficultyLevel-b.difficultyLevel);
    assert.equal(rows.length, 5);
    rows.forEach((row,index) => {
      assert.equal(row.difficultyLevel, index + 1);
      assert.equal(row.configuration.engineType, 'focus');
      const config = row.configuration.engineConfig;
      assert.equal(config.mode, 'position');
      assert.equal(config.gridSize, 3);
      assert.deepEqual([config.nLevel, config.speedMs, config.totalSteps], expected[index]);
    });
  });
}

test('daily duration impact counts repeated Focus slots separately for all 560 program days', () => {
  const impact = JSON.parse(readFileSync(new URL('../../content-packs/focus/v1/duration-impact.json', import.meta.url)));
  assert.equal(impact.length, 20);
  for (const entry of impact) {
    const programs = JSON.parse(readFileSync(new URL(`../../infrastructure/data/${entry.age}-program-plan-v2.json`, import.meta.url)));
    const program = programs.programs?.find(row => row.level === entry.level) ?? { days: programs.days };
    const [, speed, trials] = ages[entry.age][entry.level - 1];
    assert.equal(entry.scoredDurationSeconds, speed * trials / 1000);
    assert.equal(entry.days.length, 28);
    entry.days.forEach((day, index) => {
      const slots = program.days[index].filter(type => type === 'Focus').length;
      assert.equal(day.focusSlots, slots);
      assert.equal(day.focusDurationSeconds, Math.round(slots * speed * trials / 100) / 10);
      assert.equal(day.estimatedDailyMinutes, (program.days[index].length - slots) * 5 + slots * Math.ceil(speed * trials / 60000));
    });
  }
});
