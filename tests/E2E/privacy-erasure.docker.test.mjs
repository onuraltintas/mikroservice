import assert from 'node:assert/strict';
import { randomUUID } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  createMassTransitEnvelope, requireDisposableContainer, requireDisposableEnvironment,
} from './support/privacy-docker.mjs';

const assessmentContract = 'EduPlatform.Shared.Contracts.Events.Privacy:PersonalDataErasureAssessmentRequestedV1';
const executionContract = 'EduPlatform.Shared.Contracts.Events.Privacy:PersonalDataErasureExecutionRequestedV1';
const docker = process.env.DOCKER_EXE || 'docker';
const repoRoot = fileURLToPath(new URL('../..', import.meta.url));

function verifyIdentityContainer(config) {
  const compose = spawnSync(docker, ['compose', 'ps', '-q', 'identity-service'], {
    cwd: repoRoot, encoding: 'utf8', timeout: 20_000,
  });
  if (compose.error || compose.status !== 0 || !compose.stdout.trim()) {
    throw new Error('Privacy Docker E2E requires a running Compose Identity service.');
  }
  const containerId = compose.stdout.trim().split(/\r?\n/)[0];
  const inspect = spawnSync(docker, ['inspect', '--format', '{{json .Config.Env}}', containerId], {
    encoding: 'utf8', timeout: 20_000,
  });
  if (inspect.error || inspect.status !== 0) {
    throw new Error('Privacy Docker E2E could not inspect the Identity container.');
  }
  requireDisposableContainer(JSON.parse(inspect.stdout), config.ENVIRONMENT);
}

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

function sql(config, database, statement) {
  const result = spawnSync(docker, [
    'exec', '-i', 'postgres', 'psql', '-X', '-v', 'ON_ERROR_STOP=1',
    '-U', config.POSTGRES_USER, '-d', database, '-At',
  ], { input: `${statement}\n`, encoding: 'utf8', timeout: 20_000 });
  if (result.error || result.status !== 0) {
    throw new Error(`PostgreSQL E2E query failed in ${database}: ${result.stderr?.trim() || result.error?.message}`);
  }
  return result.stdout.trim();
}

async function publish(config, contract, message, requestId) {
  const envelope = createMassTransitEnvelope(contract, message, requestId);
  const credentials = Buffer.from(`${config.RABBITMQ_DEFAULT_USER}:${config.RABBITMQ_DEFAULT_PASS}`).toString('base64');
  const response = await fetch(`http://127.0.0.1:15672/api/exchanges/%2F/${encodeURIComponent(contract)}/publish`, {
    method: 'POST',
    headers: { authorization: `Basic ${credentials}`, 'content-type': 'application/json' },
    body: JSON.stringify({
      properties: { delivery_mode: 2, content_type: 'application/vnd.masstransit+json', headers: {} },
      routing_key: '',
      payload: JSON.stringify(envelope),
      payload_encoding: 'string',
    }),
    signal: AbortSignal.timeout(10_000),
  });
  if (!response.ok || !(await response.json()).routed) {
    throw new Error(`RabbitMQ did not route ${contract}.`);
  }
  return envelope.messageId;
}

async function eventually(read, expected, label) {
  const deadline = Date.now() + 45_000;
  while (Date.now() < deadline) {
    const actual = read();
    if (actual === expected) return;
    await new Promise(resolve => setTimeout(resolve, 300));
  }
  throw new Error(`${label} did not reach ${expected}.`);
}

test('disposable Docker account erasure completes across three product databases and Identity',
  { timeout: 120_000 }, async () => {
    const config = await settings();
    requireDisposableEnvironment(config);
    verifyIdentityContainer(config);
    for (const key of ['POSTGRES_USER', 'RABBITMQ_DEFAULT_USER', 'RABBITMQ_DEFAULT_PASS']) {
      assert.ok(config[key], `${key} is required`);
    }
    const identityDb = config.POSTGRES_DB_IDENTITY || 'identity_db';
    const coachingDb = config.POSTGRES_DB_COACHING || 'coaching_db';
    const notificationDb = config.POSTGRES_DB_NOTIFICATION || 'notification_db';
    const speedDb = config.POSTGRES_DB_SPEED_READING_OWNED || 'speedreading_owned_db';
    const userId = randomUUID();
    const requestId = randomUUID();
    const now = new Date().toISOString();
    const email = `privacy-e2e-${userId.replaceAll('-', '')}@example.test`;

    sql(config, identityDb, `
      INSERT INTO identity.users ("Id", "Email", "FirstName", "LastName", "PasswordHash", "PasswordSalt",
        "EmailConfirmed", "PhoneConfirmed", "IsActive", "CreatedAt", "Version", "MfaEnabled",
        "MfaFailedAttempts", "MfaRecoveryCodeHashesJson")
      VALUES ('${userId}', '${email}', 'Privacy', 'E2E', decode('01', 'hex'), decode('02', 'hex'),
        true, false, true, '${now}', 0, false, 0, '[]');
      INSERT INTO identity."DataSubjectRequests" ("Id", "RequesterUserId", "RequestType", "Scope",
        "Status", "Reason", "SubmittedAt", "IdentityVerifiedAt", "DecidedByUserId",
        "DecisionReason", "DecidedAt", "ProcessingStartedAt", "CreatedAt", "UpdatedAt", "Version")
      VALUES ('${requestId}', '${userId}', 'Erasure', 'Account', 'Processing', 'Disposable Docker E2E',
        '${now}', '${now}', '${userId}', 'E2E approval', '${now}', '${now}', '${now}', '${now}', 0);
    `);
    sql(config, coachingDb, `INSERT INTO coaching.academic_goals
      (id, student_id, title, category, current_progress, is_completed, created_at, "Version")
      VALUES (gen_random_uuid(), '${userId}', 'Privacy E2E', 'Other', 0, false, '${now}', 0);`);
    sql(config, notificationDb, `
      INSERT INTO "Notifications" ("Id", "UserId", "Title", "Message", "Type", "IsRead", "CreatedAt")
      VALUES (gen_random_uuid(), '${userId}', 'E2E', 'Delete', 'test', false, '${now}');
      INSERT INTO "EmailDeliveries" ("Id", "MessageId", "SubjectUserId", "ConsumerType", "Recipient",
        "Subject", "Body", "Status", "AttemptCount", "CreatedAt", "NextAttemptAt")
      VALUES (gen_random_uuid(), gen_random_uuid(), '${userId}', 'E2E', '${email}',
        'E2E', 'Delete', 0, 0, '${now}', '${now}');
      INSERT INTO "SupportRequests" ("Id", "FirstName", "LastName", "Email", "Subject", "Message",
        "IsProcessed", "CreatedAt", "Version", "SubjectUserId")
      VALUES (gen_random_uuid(), 'Privacy', 'E2E', '${email}', 'E2E', 'Delete', false, '${now}', 0, '${userId}');
    `);
    sql(config, speedDb, `INSERT INTO speed_reading.user_profiles
      (id, "UserId", "CurrentLevel", "TargetWPM", "TargetComprehension", "DailyGoalMinutes",
        "IsActive", created_at, version)
      VALUES (gen_random_uuid(), '${userId}', 1, 300, 80, 20, true, '${now}', 0);`);

    await publish(config, assessmentContract, {
      eventId: randomUUID(), requestId, subjectUserId: userId, approvedAt: now,
      dryRun: true, scope: 1, schemaVersion: '1.0',
    }, requestId);
    await eventually(() => sql(config, coachingDb, `SELECT count(*) FROM coaching."CoachingErasureAssessments"
      WHERE "RequestId"='${requestId}' AND "CanProceed"=true AND "GoalCount"=1;`), '1', 'Coaching assessment');

    const execution = {
      eventId: randomUUID(), requestId, subjectUserId: userId,
      authorizedAt: new Date().toISOString(), scope: 1, schemaVersion: '1.0',
    };
    await publish(config, executionContract, execution, requestId);
    await eventually(() => sql(config, identityDb, `SELECT "Status" FROM identity."DataSubjectRequests"
      WHERE "Id"='${requestId}';`), 'Completed', 'Identity erasure request');

    assert.equal(sql(config, identityDb, `SELECT string_agg("ServiceName" || ':' || "DeletedRecordCount", ','
      ORDER BY "ServiceName") FROM identity."DataSubjectRequestExecutionResults"
      WHERE "RequestId"='${requestId}';`), 'Coaching:1,Notification:3,SpeedReading:1');
    assert.equal(sql(config, identityDb, `SELECT "Email" || '|' || "IsActive" || '|' ||
      octet_length("PasswordHash") FROM identity.users WHERE "Id"='${userId}';`),
    `erased-${userId.replaceAll('-', '')}@deleted.invalid|false|0`);
    assert.equal(sql(config, coachingDb, `SELECT count(*) FROM coaching.academic_goals
      WHERE student_id='${userId}';`), '0');
    assert.equal(sql(config, notificationDb, `SELECT (SELECT count(*) FROM "Notifications" WHERE "UserId"='${userId}')
      + (SELECT count(*) FROM "EmailDeliveries" WHERE "SubjectUserId"='${userId}')
      + (SELECT count(*) FROM "SupportRequests" WHERE "SubjectUserId"='${userId}');`), '0');
    assert.equal(sql(config, speedDb, `SELECT count(*) FROM speed_reading.user_profiles
      WHERE "UserId"='${userId}';`), '0');

    const replayMessageId = await publish(
      config, executionContract, { ...execution, eventId: randomUUID() }, requestId);
    for (const [database, schema, table] of [
      [coachingDb, 'coaching', 'CoachingErasureExecutions'],
      [notificationDb, 'public', 'PrivacyErasureExecutions'],
      [speedDb, 'speed_reading', 'privacy_erasure_executions'],
    ]) {
      await eventually(() => sql(config, database, `SELECT count(*) FROM ${schema}."InboxState"
        WHERE "MessageId"='${replayMessageId}';`), '1', `${schema} replay inbox`);
      assert.equal(sql(config, database, `SELECT count(*) FROM ${schema}."${table}"
        WHERE "RequestId"='${requestId}';`), '1');
    }
  });
