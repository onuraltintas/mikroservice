import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const tailwindStyles = readFileSync(new URL('../src/tailwind.css', import.meta.url), 'utf8');

test('dark utilities follow the app theme class instead of the OS preference', () => {
  assert.match(tailwindStyles, /@custom-variant\s+dark\s+\(&:where\(\.dark,\s*\.dark \*\)\);/);
});
