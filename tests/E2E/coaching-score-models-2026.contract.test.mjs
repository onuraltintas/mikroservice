import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

// content-packs/coaching-score-models/2026/derive.py: argument handling (review C3, 01.11.2026).
//   node --test tests/E2E/coaching-score-models-2026.contract.test.mjs
const derive = fileURLToPath(new URL('../../content-packs/coaching-score-models/2026/derive.py', import.meta.url));
const run = (...args) => spawnSync('python3', ['-I', derive, ...args], { encoding: 'utf8' });

test('--help and -h print the usage and exit 0 instead of treating the flag as a file', () => {
  for (const flag of ['--help', '-h']) {
    const result = run(flag);
    assert.equal(result.status, 0, result.stderr);
    assert.match(result.stdout, /Usage: python3 -I derive\.py/);
    assert.doesNotMatch(result.stderr, /Traceback|FileNotFoundError/);
  }
});

test('missing or unknown arguments print the usage and fail without a traceback', () => {
  for (const args of [[], ['--chek', 'x.csv'], ['a.csv', 'b.csv']]) {
    const result = run(...args);
    assert.notEqual(result.status, 0, args.join(' '));
    assert.match(result.stderr, /Usage: python3 -I derive\.py/, args.join(' '));
    assert.doesNotMatch(result.stderr, /Traceback/, args.join(' '));
  }
});
