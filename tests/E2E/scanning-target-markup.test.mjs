import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

test('scanning found indicator renders without an icon font or duplicate classes', () => {
  const html = readFileSync(new URL('../../clients/speed-reading/src/app/features/student/exercises/universal-player/exercise-player.component.html', import.meta.url), 'utf8');
  const panel = html.split('<div class="scan-targets-panel">')[1].split('<div class="scan-text-area"')[0];
  assert.doesNotMatch(panel, /check_circle/);
  assert.match(panel, /<svg[^>]*class="found-icon"/);
  assert.match(panel, /aria-label="Bulundu"/);
});
