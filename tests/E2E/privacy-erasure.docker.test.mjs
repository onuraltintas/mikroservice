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

function verifyContainers(config) {
  requireDisposableEnvironment(config);
  assert.match(config.COMPOSE_PROJECT_NAME || '', /^privacy-e2e-[a-z0-9-]+$/);
  for (const service of ['identity-service', 'rabbitmq']) {
    const compose = spawnSync(docker, ['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE,
      'ps', '-q', service], { cwd: repoRoot, encoding: 'utf8', timeout: 20_000 });
    if (compose.error || compose.status !== 0 || !compose.stdout.trim()) {
      throw new Error(`Privacy Docker E2E requires a running Compose ${service} service.`);
    }
    const containerId = compose.stdout.trim().split(/\r?\n/)[0];
    const inspect = spawnSync(docker, ['inspect', '--format', '{{json .}}', containerId], {
      encoding: 'utf8', timeout: 20_000,
    });
    if (inspect.error || inspect.status !== 0) {
      throw new Error(`Privacy Docker E2E could not inspect the ${service} container.`);
    }
    const container = JSON.parse(inspect.stdout);
    assert.equal(container.Config.Labels['com.docker.compose.project'], config.COMPOSE_PROJECT_NAME);
    if (service === 'identity-service') {
      requireDisposableContainer(container.Config.Env, config.ENVIRONMENT);
    } else {
      const port = String(config.E2E_RABBITMQ_MANAGEMENT_PORT || 15672);
      assert.ok(container.NetworkSettings.Ports['15672/tcp']?.some(binding => binding.HostPort === port),
        'RabbitMQ management port must belong to the disposable Compose broker');
    }
  }
}

async function settings() {
  const file = process.env.E2E_COMPOSE_ENV_FILE || '.env.example';
  const lines = (await readFile(new URL(`../../${file}`, import.meta.url), 'utf8')).split(/\r?\n/);
  const values = Object.fromEntries(lines
    .filter(line => line && !line.startsWith('#') && line.includes('='))
    .map(line => {
      const separator = line.indexOf('=');
      return [line.slice(0, separator).trim(), line.slice(separator + 1).trim()];
    }));
  return { ...values, ...process.env, E2E_COMPOSE_ENV_FILE: file };
}

function sql(config, database, statement) {
  const result = spawnSync(docker, [
    'compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'exec', '-T', 'postgres',
    'psql', '-X', '-v', 'ON_ERROR_STOP=1',
    '-U', config.POSTGRES_USER, '-d', database, '-At',
  ], { cwd: repoRoot, input: `${statement}\n`, encoding: 'utf8', timeout: 20_000 });
  if (result.error || result.status !== 0) {
    throw new Error(`PostgreSQL E2E query failed in ${database}: ${result.stderr?.trim() || result.error?.message}`);
  }
  return result.stdout.trim();
}

function minio(config, args, input) {
  return spawnSync(docker, [
    'compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'exec', '-T', 'minio',
    'mc', ...args,
  ], { cwd: repoRoot, input, encoding: 'utf8', timeout: 20_000 });
}

function requireMinio(config, args, input) {
  const result = minio(config, args, input);
  if (result.error || result.status !== 0) {
    throw new Error(`Disposable MinIO operation failed: ${result.stderr?.trim() || result.error?.message}`);
  }
  return result.stdout.trim();
}

async function publish(config, contract, message, requestId) {
  const envelope = createMassTransitEnvelope(contract, message, requestId);
  const credentials = Buffer.from(`${config.RABBITMQ_DEFAULT_USER}:${config.RABBITMQ_DEFAULT_PASS}`).toString('base64');
  const response = await fetch(`http://127.0.0.1:${config.E2E_RABBITMQ_MANAGEMENT_PORT || 15672}/api/exchanges/%2F/${encodeURIComponent(contract)}/publish`, {
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

async function consumersReady(config) {
  const credentials = Buffer.from(`${config.RABBITMQ_DEFAULT_USER}:${config.RABBITMQ_DEFAULT_PASS}`).toString('base64');
  const response = await fetch(`http://127.0.0.1:${config.E2E_RABBITMQ_MANAGEMENT_PORT || 15672}/api/queues/%2F`, {
    headers: { authorization: `Basic ${credentials}` },
    signal: AbortSignal.timeout(10_000),
  });
  if (!response.ok) return 'false';
  const queues = new Map((await response.json()).map(queue => [queue.name, queue.state]));
  return [
    'PersonalDataErasureAssessmentRequested',
    'PersonalDataErasureAssessmentCompleted',
    'PersonalDataErasureExecutionRequested',
    'PersonalDataErasureExecutionCompleted',
    'SpeedReadingErasureAssessmentRequested',
    'SpeedReadingErasureExecutionRequested',
    'notification-privacy-erasure-assessment',
    'notification-privacy-erasure-execution',
  ].every(name => queues.get(name) === 'running').toString();
}

async function eventually(read, expected, label) {
  const deadline = Date.now() + 45_000;
  while (Date.now() < deadline) {
    const actual = await read();
    if (actual === expected) return;
    await new Promise(resolve => setTimeout(resolve, 300));
  }
  throw new Error(`${label} did not reach ${expected}.`);
}

test('disposable Docker account erasure completes across three product databases and Identity',
  { timeout: 120_000 }, async () => {
    const config = await settings();
    verifyContainers(config);
    for (const key of ['POSTGRES_USER', 'RABBITMQ_DEFAULT_USER', 'RABBITMQ_DEFAULT_PASS',
      'MINIO_ROOT_USER', 'MINIO_ROOT_PASSWORD', 'ATTACHMENT_MINIO_BUCKET']) {
      assert.ok(config[key], `${key} is required`);
    }
    await eventually(() => consumersReady(config), 'true', 'privacy consumers');
    const identityDb = config.POSTGRES_DB_IDENTITY || 'identity_db';
    const coachingDb = config.POSTGRES_DB_COACHING || 'coaching_db';
    const notificationDb = config.POSTGRES_DB_NOTIFICATION || 'notification_db';
    const speedDb = config.POSTGRES_DB_SPEED_READING_OWNED || 'speedreading_owned_db';
    const userId = randomUUID();
    const requestId = randomUUID();
    const assignmentId = randomUUID();
    const assignmentStudentId = randomUUID();
    const attachmentId = randomUUID();
    const storageKey = `privacy-e2e/${attachmentId}.jpg`;
    const otherStorageKey = `privacy-e2e/${randomUUID()}.jpg`;
    const bucket = config.ATTACHMENT_MINIO_BUCKET;
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
    sql(config, coachingDb, `
      INSERT INTO coaching.academic_goals
        (id, student_id, title, category, current_progress, is_completed, created_at, "Version")
      VALUES (gen_random_uuid(), '${userId}', 'Privacy E2E', 'Other', 0, false, '${now}', 0);
      INSERT INTO coaching.assignments
        (id, teacher_id, title, type, source, due_date, status, created_at, "Version")
      VALUES ('${assignmentId}', gen_random_uuid(), 'Privacy attachment E2E', 'Individual', 'Digital',
        '${now}', 'Active', '${now}', 0);
      INSERT INTO coaching.assignment_students (id, assignment_id, student_id, status, created_at)
      VALUES ('${assignmentStudentId}', '${assignmentId}', '${userId}', 'Assigned', '${now}');
      INSERT INTO coaching.assignment_submission_attachments
        (id, assignment_student_id, storage_key, original_file_name, content_type,
          size_bytes, sha256, status, created_at)
      VALUES ('${attachmentId}', '${assignmentStudentId}', '${storageKey}', 'privacy.jpg',
        'image/jpeg', 4, '${'a'.repeat(64)}', 'Clean', '${now}');`);
    await eventually(async () => {
      const result = minio(config, ['alias', 'set', 'privacy-e2e', 'http://127.0.0.1:9000',
        config.MINIO_ROOT_USER, config.MINIO_ROOT_PASSWORD]);
      return String(result.status === 0);
    }, 'true', 'disposable MinIO');
    requireMinio(config, ['mb', '--ignore-existing', `privacy-e2e/${bucket}`]);
    requireMinio(config, ['pipe', `privacy-e2e/${bucket}/${storageKey}`], 'jpeg');
    requireMinio(config, ['pipe', `privacy-e2e/${bucket}/${otherStorageKey}`], 'keep');
    assert.equal(minio(config, ['stat', `privacy-e2e/${bucket}/${storageKey}`]).status, 0);
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
      WHERE "RequestId"='${requestId}' AND "CanProceed"=true AND "GoalCount"=1
        AND "AssignmentCount"=1 AND "AttachmentCount"=1;`), '1', 'Coaching assessment');

    const execution = {
      eventId: randomUUID(), requestId, subjectUserId: userId,
      authorizedAt: new Date().toISOString(), scope: 1, schemaVersion: '1.0',
    };
    await publish(config, executionContract, execution, requestId);
    await eventually(() => sql(config, identityDb, `SELECT "Status" FROM identity."DataSubjectRequests"
      WHERE "Id"='${requestId}';`), 'Completed', 'Identity erasure request');

    assert.equal(sql(config, identityDb, `SELECT string_agg("ServiceName" || ':' || "DeletedRecordCount", ','
      ORDER BY "ServiceName") FROM identity."DataSubjectRequestExecutionResults"
      WHERE "RequestId"='${requestId}';`), 'Coaching:3,Notification:3,SpeedReading:1');
    assert.equal(sql(config, identityDb, `SELECT "Email" || '|' || "IsActive" || '|' ||
      octet_length("PasswordHash") FROM identity.users WHERE "Id"='${userId}';`),
    `erased-${userId.replaceAll('-', '')}@deleted.invalid|false|0`);
    assert.equal(sql(config, coachingDb, `SELECT count(*) FROM coaching.academic_goals
      WHERE student_id='${userId}';`), '0');
    assert.equal(sql(config, coachingDb, `SELECT count(*) FROM coaching.assignment_students
      WHERE student_id='${userId}';`), '0');
    assert.equal(sql(config, coachingDb, `SELECT count(*) FROM coaching.assignment_submission_attachments
      WHERE id='${attachmentId}';`), '0');
    assert.notEqual(minio(config, ['stat', `privacy-e2e/${bucket}/${storageKey}`]).status, 0,
      'Erased student object must no longer exist');
    assert.equal(minio(config, ['stat', `privacy-e2e/${bucket}/${otherStorageKey}`]).status, 0,
      'Unrelated object must survive');
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

test('disposable Docker assessment retains Coaching data under an active legal hold',
  { timeout: 120_000 }, async () => {
    const config = await settings();
    verifyContainers(config);
    await eventually(() => consumersReady(config), 'true', 'privacy consumers');
    const identityDb = config.POSTGRES_DB_IDENTITY || 'identity_db';
    const coachingDb = config.POSTGRES_DB_COACHING || 'coaching_db';
    const userId = randomUUID();
    const requestId = randomUUID();
    const now = new Date().toISOString();

    sql(config, identityDb, `
      INSERT INTO identity.users ("Id", "Email", "FirstName", "LastName", "PasswordHash", "PasswordSalt",
        "EmailConfirmed", "PhoneConfirmed", "IsActive", "CreatedAt", "Version", "MfaEnabled",
        "MfaFailedAttempts", "MfaRecoveryCodeHashesJson")
      VALUES ('${userId}', 'privacy-hold-${userId.replaceAll('-', '')}@example.test',
        'Privacy', 'Hold', decode('01', 'hex'), decode('02', 'hex'),
        true, false, true, '${now}', 0, false, 0, '[]');
      INSERT INTO identity."DataSubjectRequests" ("Id", "RequesterUserId", "RequestType", "Scope",
        "Status", "Reason", "SubmittedAt", "IdentityVerifiedAt", "DecidedByUserId",
        "DecisionReason", "DecidedAt", "ProcessingStartedAt", "CreatedAt", "UpdatedAt", "Version")
      VALUES ('${requestId}', '${userId}', 'Erasure', 'Coaching', 'Processing', 'Disposable hold E2E',
        '${now}', '${now}', '${userId}', 'E2E approval', '${now}', '${now}', '${now}', '${now}', 0);
    `);
    sql(config, coachingDb, `
      INSERT INTO coaching.academic_goals
        (id, student_id, title, category, current_progress, is_completed, created_at, "Version")
      VALUES (gen_random_uuid(), '${userId}', 'Held privacy E2E', 'Other', 0, false, '${now}', 0);
      INSERT INTO coaching."CoachingLegalHolds"
        ("Id", "SubjectUserId", "Reason", "PlacedByUserId", "PlacedAt", "CreatedAt", "Version")
      VALUES (gen_random_uuid(), '${userId}', 'Disposable E2E hold', '${userId}', '${now}', '${now}', 0);
    `);

    await publish(config, assessmentContract, {
      eventId: randomUUID(), requestId, subjectUserId: userId, approvedAt: now,
      dryRun: true, scope: 2, schemaVersion: '1.0',
    }, requestId);
    await eventually(() => sql(config, coachingDb, `SELECT count(*) FROM coaching."CoachingErasureAssessments"
      WHERE "RequestId"='${requestId}' AND "CanProceed"=false AND "HasActiveLegalHold"=true
        AND "GoalCount"=1;`), '1', 'legal-hold assessment');
    await eventually(() => sql(config, identityDb, `SELECT count(*) FROM identity."DataSubjectRequestAssessmentResults"
      WHERE "RequestId"='${requestId}' AND "ServiceName"='Coaching' AND "CanProceed"=false
        AND "HasActiveLegalHold"=true;`), '1', 'Identity legal-hold result');
    assert.equal(sql(config, coachingDb, `SELECT count(*) FROM coaching.academic_goals
      WHERE student_id='${userId}';`), '1');
    assert.notEqual(sql(config, identityDb, `SELECT "Status" FROM identity."DataSubjectRequests"
      WHERE "Id"='${requestId}';`), 'Completed');
    assert.equal(sql(config, identityDb, `SELECT "IsActive" FROM identity.users
      WHERE "Id"='${userId}';`), 't');
  });
