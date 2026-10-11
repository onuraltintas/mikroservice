import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { buildTrackingPack } from './generate.mjs';

test('all twenty age/level cells use the approved pilot duration and cycle', () => {
  const rows = buildTrackingPack();
  assert.equal(rows.length, 20);
  assert.equal(new Set(rows.map(row => row.id)).size, 20);
  const expected = {
    child: [[45,60,75,90,105], [4000,3800,3600,3400,3200]],
    teen: [[60,75,90,105,120], [3600,3400,3200,3000,2800]],
    'young-adult': [[75,90,105,120,135], [3400,3200,3000,2800,2600]],
    adult: [[90,105,120,135,150], [3200,3000,2800,2600,2400]]
  };
  for (const [age, [durations, cycles]] of Object.entries(expected)) {
    const cells = rows.filter(row => row.age === age);
    assert.deepEqual(cells.map(row => row.level), [1,2,3,4,5]);
    assert.deepEqual(cells.map(row => row.configuration.engineConfig.timing.durationSeconds), durations);
    assert.deepEqual(cells.map(row => row.configuration.engineConfig.timing.speedMs), cycles);
    assert.deepEqual(cells.map(row => row.configuration.engineConfig.path.type), ['horizontal','circle','circle','infinity8','infinity8']);
    for (const row of cells) {
      const old = structuredClone(row.previousConfiguration), next = structuredClone(row.configuration);
      for (const config of [old, next]) {
        delete config.engineConfig.timing.durationSeconds;
        delete config.engineConfig.timing.speedMs;
        delete config.engineConfig.path.type;
      }
      assert.deepEqual(next, old);
      assert.equal(row.configuration.engineConfig.content.type, 'dot');
    }
  }
});

test('checked-in JSON and SQL match the generator and do not delete any history', () => {
  const settings = JSON.stringify(buildTrackingPack(), null, 2);
  const file = name => readFileSync(new URL(name, import.meta.url), 'utf8').replace(/\r\n/g, '\n');
  assert.equal(file('settings.json'), settings + '\n');
  for (const name of ['apply','rollback']) {
    const sql = file(`${name}.sql`);
    assert.equal(sql, file(`${name}.template.sql`).replace('{{SETTINGS}}', settings));
    assert.doesNotMatch(sql, /\b(DELETE FROM|TRUNCATE|DROP TABLE)\b/i);
    assert.match(sql, /baseline changed/i);
  }
});
