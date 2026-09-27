import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const html = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.html', import.meta.url), 'utf8');
const scss = readFileSync(new URL('../src/app/features/student/exercises/universal-player/exercise-player.component.scss', import.meta.url), 'utf8');

test('Schulte results keep metrics and heatmap in one compact section', () => {
  const section = html.match(/<section class="schulte-summary"[\s\S]*?<\/section>/)?.[0];
  assert.ok(section, 'Schulte summary must exist');
  for (const value of ['result?.score', 'result?.accuracy', 'result?.errors', 'result?.totalTime', 'heatmapData', 'benchmarks']) {
    assert.ok(section.includes(value), `${value} must be inside the same summary`);
  }
  assert.equal((html.match(/class="heatmap-grid-new"/g) ?? []).length, 1);
});

test('Schulte compact section is responsive and other results retain their layout', () => {
  assert.match(html, /class="result-card-new"\s+\[class\.result-card-new--schulte\]="engine\?\.engineType === 'grid_interaction'"/);
  assert.match(scss, /\.schulte-summary[\s\S]*?@media \(max-width: 768px\)/);
});

test('all exercise result types share one compact visual system', () => {
  assert.match(html, /class="result-card-new"[^>]*\[class\.result-card-new--schulte\]/);
  assert.match(html, /class="result-eyebrow"/);
  assert.match(scss, /\.result-card-new\s*\{[\s\S]*?max-width:\s*900px/);
  assert.match(scss, /\.result-card-new\s+\.speed-reading-results\s*\{/);
  assert.match(scss.slice(scss.indexOf('// Shared result surface')), /\.action-row\s*\{[\s\S]*?border-top:\s*1px solid/);
});
