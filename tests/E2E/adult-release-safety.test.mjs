import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const root = new URL('../../tools/releases/adult-programs-v2-20261010/', import.meta.url);
const shell = process.platform === 'win32' ? 'C:/Program Files/Git/bin/bash.exe' : 'bash';
const run = source => spawnSync(shell, ['-c', source], { encoding: 'utf8' });

for (const [name, stopped, applyStarted, action] of [
  ['failed stop acknowledgement restores the pre-apply API', 1, 0, 'start'],
  ['ambiguous apply acknowledgement fails closed', 1, 1, 'stop'],
  ['failed post-start verification stops the API again', 0, 1, 'stop'],
]) {
  test(name, () => {
    const source = readFileSync(new URL('deploy.sh', root), 'utf8');
    const handler = source.slice(source.indexOf('on_exit() {'), source.indexOf('trap on_exit EXIT'));
    const result = run(`set -e
api=test-only-api
stopped=${stopped}
apply_started=${applyStarted}
committed=0
docker() { printf 'ACTION:%s\\n' "$1" >&2; }
${handler}
trap on_exit EXIT
false`);
    assert.equal(result.status, 1);
    assert.ok(result.stderr.includes(`ACTION:${action}`), result.stderr);
    assert.ok(!result.stderr.includes(`ACTION:${action === 'stop' ? 'start' : 'stop'}`), result.stderr);
  });
}

test('stop and apply intent are recorded before invoking external commands', () => {
  const source = readFileSync(new URL('deploy.sh', root), 'utf8');
  assert.ok(source.indexOf('flock -n 9') < source.indexOf('trap on_exit EXIT'));
  const apply = source.indexOf('psql_run -v plan_json=');
  assert.ok(source.lastIndexOf('apply_started=1', apply) > source.indexOf('psql_run()'));
  const stop = source.indexOf('docker stop --time 30 "$api" >/dev/null', source.indexOf('curl --fail'));
  assert.ok(source.lastIndexOf('stopped=1', stop) > source.indexOf('psql_run()'));
});

for (const [name, mock, success] of [
  ['log-read failure must fail monitoring', 'return 1', false],
  ['severe log marker must fail monitoring', "printf 'fail: simulated failure\\n'", false],
  ['severe marker in a large log must fail monitoring', "printf 'fail: simulated failure\\n'; printf 'info: simulated log\\n%.0s' {1..20000}", false],
  ['successfully read clean logs pass monitoring', "printf 'info: simulated healthy log\\n'", true],
]) {
  test(name, () => {
    const source = readFileSync(new URL('monitor.sh', root), 'utf8');
    const tail = source.slice(source.indexOf('\ndone\n') + '\ndone\n'.length);
    const result = run(`set -euo pipefail
api=test-only-api
monitor_start=test-only
docker() { ${mock}; }
${tail}`);
    assert.equal(result.status === 0, success, result.stderr);
    assert.equal(result.stdout.includes('MONITOR_OK'), success);
  });
}
