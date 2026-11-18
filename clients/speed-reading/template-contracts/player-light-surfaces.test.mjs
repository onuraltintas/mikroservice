import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const dir = new URL('../src/app/features/student/exercises/universal-player/', import.meta.url);
const template = readFileSync(new URL('exercise-player.component.html', dir), 'utf8');
const styles = readFileSync(new URL('exercise-player.component.scss', dir), 'utf8');

test('player elements never carry two class attributes (Angular keeps only the last one)', () => {
  const duplicates = template.split('\n').filter((line) => /class="[^"]*"\s+class="/.test(line));
  assert.deepEqual(duplicates, []);
});

test('the start button keeps its primary button classes', () => {
  assert.match(template, /class="ep-btn ep-btn--primary start-button-large"/);
});

test('light exercise surfaces pin light text tokens for the dark theme', () => {
  const block = styles.match(/\.start-card,\s*\.game-container,\s*\.result-card-new,\s*\.exit-confirm-card,\s*\.completion-card\s*\{([^}]*)\}/);
  assert.ok(block, 'light surface token block is missing');
  for (const token of ['--sp-text-1: #0f172a', '--sp-surface: #ffffff', '--sp-text-inv: #ffffff', 'color: var(--sp-text-1)']) {
    assert.ok(block[1].includes(token), `missing ${token}`);
  }
});
