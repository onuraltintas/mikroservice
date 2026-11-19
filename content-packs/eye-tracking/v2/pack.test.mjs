import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { buildTrackingPack } from './generate.mjs';

test('children train 45 seconds and the other ages 60 seconds at every level; nothing else changes', () => {
  const rows = buildTrackingPack();
  assert.equal(rows.length, 20);
  assert.equal(new Set(rows.map(row => row.id)).size, 20);
  const expected = { child: 45, teen: 60, 'young-adult': 60, adult: 60 };
  for (const [age, seconds] of Object.entries(expected)) {
    const cells = rows.filter(row => row.age === age);
    assert.deepEqual(cells.map(row => row.level), [1, 2, 3, 4, 5]);
    assert.deepEqual(cells.map(row => row.configuration.engineConfig.timing.durationSeconds), [seconds, seconds, seconds, seconds, seconds]);
    // The level still differs by its path and its cycle, as in v1.
    assert.deepEqual(cells.map(row => row.configuration.engineConfig.path.type), ['horizontal', 'circle', 'circle', 'infinity8', 'infinity8']);
    for (const row of cells) {
      const before = structuredClone(row.previousConfiguration), after = structuredClone(row.configuration);
      delete before.engineConfig.timing.durationSeconds;
      delete after.engineConfig.timing.durationSeconds;
      assert.deepEqual(after, before);
    }
  }
});

test('the baseline is the live v1 setting', () => {
  const v1 = JSON.parse(readFileSync(new URL('../v1/settings.json', import.meta.url), 'utf8'));
  const rows = buildTrackingPack();
  for (const row of rows) assert.deepEqual(row.previousConfiguration, v1.find(item => item.id === row.id).configuration);
});

test('checked-in JSON and SQL match the generator and delete no history', () => {
  const settings = JSON.stringify(buildTrackingPack(), null, 2);
  const file = name => readFileSync(new URL(name, import.meta.url), 'utf8').replace(/\r\n/g, '\n');
  assert.equal(file('settings.json'), settings + '\n');
  for (const name of ['apply', 'rollback']) {
    const sql = file(`${name}.sql`);
    assert.equal(sql, file(`${name}.template.sql`).replace('{{SETTINGS}}', settings));
    assert.doesNotMatch(sql, /\b(DELETE FROM|TRUNCATE|DROP TABLE)\b/i);
    assert.match(sql, /baseline changed/i);
  }
});
