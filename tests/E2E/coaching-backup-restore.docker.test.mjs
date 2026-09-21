import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { requireDisposableContainer, requireDisposableEnvironment } from './support/privacy-docker.mjs';

const repoRoot = fileURLToPath(new URL('../..', import.meta.url));
const docker = process.env.DOCKER_EXE || 'docker';

async function settings() {
  let local = '';
  try {
    local = await readFile(new URL('../../.env', import.meta.url), 'utf8');
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
  }
  const values = Object.fromEntries(local.split(/\r?\n/)
    .filter(line => line && !line.startsWith('#') && line.includes('='))
    .map(line => {
      const separator = line.indexOf('=');
      return [line.slice(0, separator).trim(), line.slice(separator + 1).trim()];
    }));
  return { ...values, ...process.env };
}

function dockerCommand(config, args, input) {
  const result = spawnSync(docker, [
    'compose', '--env-file', config.E2E_COMPOSE_ENV_FILE || '.env', 'exec', '-T', 'postgres', ...args,
  ], { cwd: repoRoot, input, timeout: 60_000, maxBuffer: 100 * 1024 * 1024 });
  if (result.error || result.status !== 0) {
    throw new Error(`Disposable PostgreSQL drill failed (${args[0]}): ${result.stderr?.toString().trim() || result.error?.message}`);
  }
  return result.stdout;
}

function verifyDevelopmentContainer(config) {
  requireDisposableEnvironment(config);
  assert.equal(config.ENVIRONMENT, 'Development', 'Backup/restore drill is limited to Development.');
  const compose = spawnSync(docker, [
    'compose', '--env-file', config.E2E_COMPOSE_ENV_FILE || '.env', 'ps', '-q', 'identity-service',
  ], { cwd: repoRoot, encoding: 'utf8', timeout: 20_000 });
  assert.equal(compose.status, 0);
  const containerId = compose.stdout.trim().split(/\r?\n/)[0];
  assert.ok(containerId, 'A running disposable Identity container is required.');
  const inspect = spawnSync(docker, ['inspect', '--format', '{{json .Config.Env}}', containerId], {
    encoding: 'utf8', timeout: 20_000,
  });
  assert.equal(inspect.status, 0);
  requireDisposableContainer(JSON.parse(inspect.stdout), 'Development');
}

test('disposable Coaching schema backup restores a synthetic goal into a fresh database',
  { timeout: 180_000 }, async () => {
    const config = await settings();
    verifyDevelopmentContainer(config);
    assert.ok(config.POSTGRES_USER, 'POSTGRES_USER is required.');
    const database = config.POSTGRES_DB_COACHING || 'coaching_db';
    const suffix = randomUUID().replaceAll('-', '').slice(0, 16);
    const source = `coaching_drill_source_${suffix}`;
    const restored = `coaching_drill_restored_${suffix}`;
    const studentId = randomUUID();
    const user = config.POSTGRES_USER;
    let sourceCreated = false;
    let restoredCreated = false;

    try {
      // Schema-only export avoids copying any existing user's Coaching records.
      const schema = dockerCommand(config, [
        'pg_dump', '-Fc', '--schema-only', '--no-owner', '--no-acl', '-U', user, '-d', database,
      ]);
      assert.ok(schema.length > 0);
      dockerCommand(config, ['createdb', '-U', user, source]);
      sourceCreated = true;
      dockerCommand(config, ['pg_restore', '--exit-on-error', '--no-owner', '--no-acl',
        '-U', user, '-d', source], schema);
      dockerCommand(config, ['psql', '-X', '-v', 'ON_ERROR_STOP=1', '-U', user, '-d', source],
        Buffer.from(`INSERT INTO coaching.academic_goals
          (id, student_id, title, category, current_progress, is_completed, created_at, "Version")
          VALUES (gen_random_uuid(), '${studentId}', 'Restore drill', 'Other', 0, false, now(), 0);\n`));

      const backup = dockerCommand(config, [
        'pg_dump', '-Fc', '--no-owner', '--no-acl', '-U', user, '-d', source,
      ]);
      assert.ok(backup.length > schema.length);
      dockerCommand(config, ['createdb', '-U', user, restored]);
      restoredCreated = true;
      dockerCommand(config, ['pg_restore', '--exit-on-error', '--no-owner', '--no-acl',
        '-U', user, '-d', restored], backup);
      const count = dockerCommand(config, ['psql', '-X', '-At', '-v', 'ON_ERROR_STOP=1',
        '-U', user, '-d', restored, '-c', `SELECT count(*) FROM coaching.academic_goals
          WHERE student_id='${studentId}' AND title='Restore drill';`]);
      assert.equal(count.toString().trim(), '1');
    } finally {
      if (restoredCreated) dockerCommand(config, ['dropdb', '--if-exists', '-U', user, restored]);
      if (sourceCreated) dockerCommand(config, ['dropdb', '--if-exists', '-U', user, source]);
    }
  });
