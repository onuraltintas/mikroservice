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

test('exercise surfaces follow the theme through tokens, with no fixed colours (redesign 18.11.2026)', () => {
  // The 18.11.2026 bug: white text from the dark theme on a white card. Now the cards themselves follow the theme,
  // so no surface may name a colour of its own.
  const block = styles.match(/\.start-card,\s*\.game-container,\s*\.result-card-new,\s*\.exit-confirm-card,\s*\.completion-card\s*\{([^}]*)\}/);
  assert.ok(block, 'exercise surface block is missing');
  assert.ok(block[1].includes('color: var(--sp-text-1)'));
  const fixed = styles.split('\n').filter((line) => /^\s*[a-z-]+\s*:[^;]*(#[0-9a-fA-F]{3,8}\b|rgba?\(\s*\d)/.test(line));
  assert.deepEqual(fixed, []);
});
