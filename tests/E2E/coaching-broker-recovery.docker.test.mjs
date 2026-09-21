import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  createMassTransitEnvelope, requireDisposableContainer, requireDisposableEnvironment,
} from './support/privacy-docker.mjs';

const contract = 'EduPlatform.Shared.Contracts.Events.Privacy:PersonalDataErasureAssessmentRequestedV1';
const queueName = 'PersonalDataErasureAssessmentRequested';
const docker = process.env.DOCKER_EXE || 'docker';
const repoRoot = fileURLToPath(new URL('../..', import.meta.url));

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
  const result = spawnSync(docker, args, { cwd: repoRoot, input, encoding: 'utf8', timeout: 30_000 });
  if (result.error || result.status !== 0) {
    throw new Error(`Disposable broker E2E Docker command failed: ${result.stderr?.trim() || result.error?.message}`);
  }
  return result.stdout.trim();
}

function compose(config, args) {
  return command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, ...args]);
}

function verifyContainers(config) {
  requireDisposableEnvironment(config);
  assert.equal(config.ENVIRONMENT, 'Development');
  assert.match(config.COMPOSE_PROJECT_NAME || '', /^privacy-e2e-[a-z0-9-]+$/);
  for (const service of ['identity-service', 'coaching-service', 'rabbitmq', 'postgres']) {
    const id = compose(config, ['ps', '-q', service]);
    assert.ok(id, `${service} must run in the disposable Compose project`);
    const container = JSON.parse(command(['inspect', '--format', '{{json .}}', id]));
    assert.equal(container.Config.Labels['com.docker.compose.project'], config.COMPOSE_PROJECT_NAME);
    if (service === 'identity-service' || service === 'coaching-service') {
      requireDisposableContainer(container.Config.Env, 'Development');
    }
  }
}

function query(config, database, statement) {
  return command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'exec', '-T', 'postgres',
    'psql', '-X', '-v', 'ON_ERROR_STOP=1', '-U', config.POSTGRES_USER, '-d', database, '-At'],
  `${statement}\n`);
}

function queuedMessages(config) {
  const rows = compose(config, ['exec', '-T', 'rabbitmq', 'rabbitmqctl', 'list_queues', '-q',
    'name', 'messages_ready']).split(/\r?\n/);
  const row = rows.find(value => value.split(/\s+/)[0] === queueName);
  return Number(row?.split(/\s+/)[1]);
}

async function rabbit(config, path, body) {
  const credentials = Buffer.from(`${config.RABBITMQ_DEFAULT_USER}:${config.RABBITMQ_DEFAULT_PASS}`).toString('base64');
  const response = await fetch(`http://127.0.0.1:${config.E2E_RABBITMQ_MANAGEMENT_PORT || 15672}/api/${path}`, {
    method: body ? 'POST' : 'GET',
    headers: { authorization: `Basic ${credentials}`, 'content-type': 'application/json' },
    body: body && JSON.stringify(body),
    signal: AbortSignal.timeout(5_000),
  });
  assert.ok(response.ok, `RabbitMQ API ${path} returned ${response.status}`);
  return response.json();
}

async function eventually(check, label) {
  const deadline = Date.now() + 90_000;
  while (Date.now() < deadline) {
    try {
      if (await check()) return;
    } catch {
      // Services can be temporarily unavailable during broker restart.
    }
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  throw new Error(`${label} did not recover within 90 seconds.`);
}

test('disposable Coaching consumer processes a persistent assessment after RabbitMQ restart',
  { timeout: 240_000 }, async () => {
    const config = await settings();
    verifyContainers(config);
    for (const key of ['POSTGRES_USER', 'RABBITMQ_DEFAULT_USER', 'RABBITMQ_DEFAULT_PASS']) {
      assert.ok(config[key], `${key} is required`);
    }
    const coachingDb = config.POSTGRES_DB_COACHING || 'coaching_db';
    const identityDb = config.POSTGRES_DB_IDENTITY || 'identity_db';
    const userId = randomUUID();
    const requestId = randomUUID();
    const now = new Date().toISOString();
    let coachingStopped = false;
    let rabbitStopped = false;

    try {
      await eventually(async () => (await rabbit(config, `queues/%2F/${queueName}`)).state === 'running',
        'Coaching assessment queue');
      await eventually(() => query(config, coachingDb,
        `SELECT to_regclass('coaching.academic_goals') IS NOT NULL;`) === 't', 'Coaching migration');
      query(config, identityDb, `BEGIN;
        INSERT INTO identity.users ("Id", "Email", "FirstName", "LastName", "PasswordHash", "PasswordSalt",
          "EmailConfirmed", "PhoneConfirmed", "IsActive", "CreatedAt", "Version", "MfaEnabled",
          "MfaFailedAttempts", "MfaRecoveryCodeHashesJson")
        VALUES ('${userId}', 'broker-${userId.replaceAll('-', '')}@example.test', 'Broker', 'E2E',
          decode('01', 'hex'), decode('02', 'hex'), true, false, true, '${now}', 0, false, 0, '[]');
        INSERT INTO identity."DataSubjectRequests" ("Id", "RequesterUserId", "RequestType", "Scope",
          "Status", "Reason", "SubmittedAt", "IdentityVerifiedAt", "DecidedByUserId",
          "DecisionReason", "DecidedAt", "ProcessingStartedAt", "CreatedAt", "UpdatedAt", "Version")
        VALUES ('${requestId}', '${userId}', 'Erasure', 'Coaching', 'Processing', 'Broker recovery E2E',
          '${now}', '${now}', '${userId}', 'E2E approval', '${now}', '${now}', '${now}', '${now}', 0);
        COMMIT;`);
      query(config, coachingDb, `INSERT INTO coaching.academic_goals
        (id, student_id, title, category, current_progress, is_completed, created_at, "Version")
        VALUES (gen_random_uuid(), '${userId}', 'Broker recovery E2E', 'Other', 0, false, '${now}', 0);`);

      compose(config, ['stop', 'coaching-service']);
      coachingStopped = true;
      const envelope = createMassTransitEnvelope(contract, {
        eventId: randomUUID(), requestId, subjectUserId: userId, approvedAt: now,
        dryRun: true, scope: 2, schemaVersion: '1.0',
      }, requestId);
      const published = await rabbit(config, `exchanges/%2F/${encodeURIComponent(contract)}/publish`, {
        properties: { delivery_mode: 2, content_type: 'application/vnd.masstransit+json', headers: {} },
        routing_key: '', payload: JSON.stringify(envelope), payload_encoding: 'string',
      });
      assert.equal(published.routed, true);
      await eventually(() => queuedMessages(config) === 1,
        'Queued assessment before broker restart');

      compose(config, ['stop', 'rabbitmq']);
      rabbitStopped = true;
      compose(config, ['start', 'rabbitmq']);
      rabbitStopped = false;
      await eventually(() => queuedMessages(config) === 1,
        'Durable assessment after broker restart');

      compose(config, ['start', 'coaching-service']);
      coachingStopped = false;
      await eventually(() => query(config, coachingDb, `SELECT count(*) FROM coaching."CoachingErasureAssessments"
        WHERE "RequestId"='${requestId}' AND "CanProceed"=true AND "GoalCount"=1;`) === '1',
      'Coaching assessment delivery');
      await eventually(() => query(config, identityDb, `SELECT count(*) FROM identity."DataSubjectRequestAssessmentResults"
        WHERE "RequestId"='${requestId}' AND "ServiceName"='Coaching' AND "GoalCount"=1;`) === '1',
      'Identity completion delivery');
      assert.equal(query(config, coachingDb, `SELECT count(*) FROM coaching.academic_goals
        WHERE student_id='${userId}';`), '1');
    } finally {
      if (rabbitStopped) compose(config, ['start', 'rabbitmq']);
      if (coachingStopped) compose(config, ['start', 'coaching-service']);
    }
  });
