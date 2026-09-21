import assert from 'node:assert/strict';
import { createHmac, randomUUID } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { requireDisposableContainer, requireDisposableEnvironment } from './support/privacy-docker.mjs';

const docker = process.env.DOCKER_EXE || 'docker';
const repoRoot = fileURLToPath(new URL('../..', import.meta.url));
const roleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

async function settings() {
  const file = process.env.E2E_COMPOSE_ENV_FILE || '.env.example';
  const lines = (await readFile(new URL(`../../${file}`, import.meta.url), 'utf8')).split(/\r?\n/);
  const values = Object.fromEntries(lines
    .filter(line => line && !line.startsWith('#') && line.includes('='))
    .map(line => {
      const separator = line.indexOf('=');
      return [line.slice(0, separator).trim(), line.slice(separator + 1).trim()];
    }));
  return { ...values, ...process.env };
}

function command(args, input) {
  const result = spawnSync(docker, args, { cwd: repoRoot, input, encoding: 'utf8', timeout: 20_000 });
  if (result.error || result.status !== 0) {
    throw new Error(`Disposable export E2E Docker command failed: ${result.stderr?.trim() || result.error?.message}`);
  }
  return result.stdout.trim();
}

function verifyContainers(config) {
  requireDisposableEnvironment(config);
  assert.equal(config.ENVIRONMENT, 'Development');
  assert.match(config.COMPOSE_PROJECT_NAME || '', /^privacy-e2e-[a-z0-9-]+$/);
  for (const service of ['identity-service', 'coaching-service', 'api-gateway']) {
    const id = command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'ps', '-q', service]);
    assert.ok(id, `${service} must be running in the disposable Compose project`);
    const container = JSON.parse(command(['inspect', '--format', '{{json .}}', id]));
    assert.equal(container.Config.Labels['com.docker.compose.project'], config.COMPOSE_PROJECT_NAME);
    requireDisposableContainer(container.Config.Env, 'Development');
    if (service === 'api-gateway') {
      assert.ok(container.Config.Env.includes(`JWT_SECRET=${config.JWT_SECRET}`), 'JWT key must match gateway');
    }
  }
}

function sql(config, database, statement) {
  return command([
    'compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'exec', '-T', 'postgres',
    'psql', '-X', '-v', 'ON_ERROR_STOP=1', '-U', config.POSTGRES_USER, '-d', database, '-At',
  ], `${statement}\n`);
}

function token(config, userId, role) {
  const now = Math.floor(Date.now() / 1000);
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const unsigned = `${encode({ alg: 'HS256', typ: 'JWT', kid: config.JWT_KEY_ID })}.${encode({
    sub: userId, iss: config.JWT_ISSUER, aud: config.JWT_AUDIENCE,
    iat: now, nbf: now, exp: now + 300, [roleClaim]: role,
  })}`;
  return `${unsigned}.${createHmac('sha256', config.JWT_SECRET).update(unsigned).digest('base64url')}`;
}

async function request(config, bearer) {
  return fetch(`http://127.0.0.1:${config.GATEWAY_PORT}/api/data-privacy/export`, {
    headers: bearer ? { authorization: `Bearer ${bearer}` } : {},
    signal: AbortSignal.timeout(10_000),
  });
}

async function eventually(check, label) {
  const deadline = Date.now() + 60_000;
  while (Date.now() < deadline) {
    try {
      if (await check()) return;
    } catch {
      // Container startup and migrations can still be in progress.
    }
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  throw new Error(`${label} did not become ready within 60 seconds.`);
}

test('disposable Gateway exports only the authenticated student’s Coaching records',
  { timeout: 120_000 }, async () => {
    const config = await settings();
    verifyContainers(config);
    for (const key of ['POSTGRES_USER', 'JWT_SECRET', 'JWT_ISSUER', 'JWT_AUDIENCE', 'GATEWAY_PORT']) {
      assert.ok(config[key], `${key} is required`);
    }
    const studentId = randomUUID();
    const otherId = randomUUID();
    const identityDb = config.POSTGRES_DB_IDENTITY || 'identity_db';
    const coachingDb = config.POSTGRES_DB_COACHING || 'coaching_db';
    const email = `coaching-export-${studentId.replaceAll('-', '')}@example.test`;
    const now = new Date().toISOString();
    let seeded = false;

    try {
      await eventually(async () => (await fetch(
        `http://127.0.0.1:${config.GATEWAY_PORT}/health/live`,
        { signal: AbortSignal.timeout(2_000) })).ok, 'Gateway');
      await eventually(() => sql(config, identityDb,
        `SELECT count(*) FROM identity."Roles" WHERE "Name"='Student' AND "IsDeleted"=false;`) === '1',
      'Identity seed');
      await eventually(() => sql(config, coachingDb,
        `SELECT to_regclass('coaching.academic_goals') IS NOT NULL;`) === 't',
      'Coaching migration');
      assert.equal((await request(config)).status, 401);
      sql(config, identityDb, `BEGIN;
        INSERT INTO identity.users ("Id", "Email", "FirstName", "LastName", "PasswordHash", "PasswordSalt",
          "EmailConfirmed", "PhoneConfirmed", "IsActive", "CreatedAt", "Version", "MfaEnabled",
          "MfaFailedAttempts", "MfaRecoveryCodeHashesJson")
        VALUES ('${studentId}', '${email}', 'Export', 'E2E', decode('01', 'hex'), decode('02', 'hex'),
          true, false, true, '${now}', 0, false, 0, '[]');
        INSERT INTO identity."UserRoles" ("Id", "UserId", "RoleId", "CreatedAt")
        SELECT gen_random_uuid(), '${studentId}', "Id", '${now}'
          FROM identity."Roles" WHERE "Name"='Student' AND "IsDeleted"=false;
        INSERT INTO identity.student_profiles ("Id", "UserId", "FirstName", "LastName",
          "DailyGoalMinutes", "Preferences", "IsActive", "CreatedAt", "Version")
        VALUES (gen_random_uuid(), '${studentId}', 'Export', 'E2E', 30, '{}', true, '${now}', 0);
        COMMIT;
      `);
      seeded = true;
      sql(config, coachingDb, `INSERT INTO coaching.academic_goals
        (id, student_id, title, category, current_progress, is_completed, created_at, "Version")
        VALUES (gen_random_uuid(), '${studentId}', 'Own export goal', 'Other', 0, false, '${now}', 0),
          (gen_random_uuid(), '${otherId}', 'Other student secret', 'Other', 0, false, '${now}', 0);`);

      assert.equal((await request(config, token(config, studentId, 'Teacher'))).status, 403);
      assert.equal((await request(config, token(config, otherId, 'Student'))).status, 403);
      const response = await request(config, token(config, studentId, 'Student'));
      if (response.status !== 200) {
        assert.fail(`student export returned ${response.status}: ${await response.text()}`);
      }
      assert.match(response.headers.get('cache-control') || '', /no-store/);
      const exportData = await response.json();
      assert.equal(exportData.studentId.toLowerCase(), studentId);
      assert.deepEqual(exportData.goals.map(goal => goal.title), ['Own export goal']);
    } finally {
      if (seeded) {
        sql(config, coachingDb, `DELETE FROM coaching.academic_goals WHERE student_id IN ('${studentId}', '${otherId}');`);
        sql(config, identityDb, `DELETE FROM identity.users WHERE "Id"='${studentId}';`);
      }
    }
  });
