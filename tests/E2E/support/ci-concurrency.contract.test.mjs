import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// E5 (coordinator, 06.11.2026): a new push to a pull request cancels that pull request's older CI run. Runs on main
// (push) are never cancelled or replaced, because Production images builds from each green main run.
const read = file => readFileSync(new URL(`../../../${file}`, import.meta.url), 'utf8');
const ci = read('.github/workflows/ci.yml');

test('CI cancels older runs of the same pull request only', () => {
  const block = /\nconcurrency:\n((?:  .+\n)+)/.exec(ci);
  assert.ok(block, 'ci.yml has a workflow-level concurrency block');
  const group = /group: (.+)/.exec(block[1])[1];
  const cancel = /cancel-in-progress: (.+)/.exec(block[1])[1];
  assert.match(group, /github\.workflow/);
  assert.match(group, /github\.event\.pull_request\.number \|\| github\.run_id/,
    'non-PR runs get their own group, so a main run is never queued behind or replaced by a later one');
  assert.equal(cancel.trim(), "${{ github.event_name == 'pull_request' }}");
});

test('Production images still follows every completed main CI run', () => {
  const images = read('.github/workflows/production-images.yml');
  assert.match(images, /workflows: \["EduPlatform CI"\]/);
  assert.match(images, /cancel-in-progress: false/);
  assert.match(ci, /^name: EduPlatform CI$/m);
});
