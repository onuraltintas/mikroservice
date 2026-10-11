import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const script = readFileSync(new URL('./monitor.sh', import.meta.url), 'utf8').replace(/\r\n/g, '\n');
const tail = script.slice(script.indexOf('\nmarkers=') >= 0 && !script.includes('\nlogs=')
  ? script.indexOf('\nmarkers=') : script.indexOf('\nlogs='));
const gitBash = 'C:/Program Files/Git/bin/bash.exe';
const bash = process.platform === 'win32' && existsSync(gitBash) ? gitBash : 'bash';

function runLogCheck(log, exitCode = 0) {
  // Exercise the actual final monitor gate; fake only Docker, not the gate logic.
  return spawnSync(bash, ['-s'], {
    input: `set -euo pipefail\nsince=test\nbackend=test\ndocker() { printf '%s\\n' '${log}'; return ${exitCode}; }\n${tail}`,
    encoding: 'utf8',
  });
}

test('readable logs without severe markers pass', () => {
  const result = runLogCheck('Healthy');
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /MONITOR_OK/);
});

test('severe log markers reject the release monitor', () => {
  const result = runLogCheck('[ERR] Failed request');
  assert.equal(result.status, 1, result.stderr);
  assert.doesNotMatch(result.stdout, /MONITOR_OK/);
});

test('failed Docker log reads cannot claim successful monitoring', () => {
  const result = runLogCheck('Docker daemon unavailable', 1);
  assert.equal(result.status, 1, result.stderr);
  assert.doesNotMatch(result.stdout, /MONITOR_OK/);
});
