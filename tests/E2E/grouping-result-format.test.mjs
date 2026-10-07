import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

test('grouping presentation tempo is formatted without decimal places', () => {
  const html = readFileSync(new URL('../../clients/speed-reading/src/app/features/student/exercises/universal-player/exercise-player.component.html', import.meta.url), 'utf8');
  const value = html.slice(0, html.indexOf('Gösterim temposu (kelime/dk)')).split('stat-value').pop();
  assert.match(value, /number:'1\.0-0'/);
});
