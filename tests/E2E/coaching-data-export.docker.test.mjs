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

async function request(config, bearer, timeoutMs = 10_000) {
  return fetch(`http://127.0.0.1:${config.GATEWAY_PORT}/api/data-privacy/export`, {
    headers: bearer ? { authorization: `Bearer ${bearer}` } : {},
    signal: AbortSignal.timeout(timeoutMs),
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
  { timeout: 180_000 }, async () => {
    const config = await settings();
    verifyContainers(config);
    for (const key of ['POSTGRES_USER', 'JWT_SECRET', 'JWT_ISSUER', 'JWT_AUDIENCE', 'GATEWAY_PORT']) {
      assert.ok(config[key], `${key} is required`);
    }
    const studentId = randomUUID();
    const otherId = randomUUID();
    const unrelatedId = randomUUID();
    const ownInstitutionId = randomUUID();
    const otherInstitutionId = randomUUID();
    const teacherId = randomUUID();
    const privateSessionId = randomUUID();
    const sharedSessionId = randomUUID();
    const otherSessionId = randomUUID();
    const ownAssignmentId = randomUUID();
    const otherAssignmentId = randomUUID();
    const ownExamId = randomUUID();
    const otherExamId = randomUUID();
    const agreementDocumentId = randomUUID();
    const ownAcknowledgementId = randomUUID();
    const otherAcknowledgementId = randomUUID();
    const identityDb = config.POSTGRES_DB_IDENTITY || 'identity_db';
    const coachingDb = config.POSTGRES_DB_COACHING || 'coaching_db';
    const email = `coaching-export-${studentId.replaceAll('-', '')}@example.test`;
    const now = new Date().toISOString();
    let seeded = false;
    let identityStopped = false;
    let postgresStopped = false;
    let rabbitStopped = false;

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
        INSERT INTO identity.institutions
          ("Id", "Name", "Type", "LicenseType", "MaxStudents", "MaxTeachers",
            "IsActive", "CreatedAt", "Version")
        VALUES
          ('${ownInstitutionId}', 'Export E2E Institution A', 'School', 'Trial', 50, 5,
            true, '${now}', 0),
          ('${otherInstitutionId}', 'Export E2E Institution B', 'School', 'Trial', 50, 5,
            true, '${now}', 0);
        INSERT INTO identity.users ("Id", "Email", "FirstName", "LastName", "PasswordHash", "PasswordSalt",
          "EmailConfirmed", "PhoneConfirmed", "IsActive", "CreatedAt", "Version", "MfaEnabled",
          "MfaFailedAttempts", "MfaRecoveryCodeHashesJson")
        VALUES
          ('${studentId}', '${email}', 'Export', 'E2E', decode('01', 'hex'), decode('02', 'hex'),
            true, false, true, '${now}', 0, false, 0, '[]'),
          ('${otherId}', 'other-${email}', 'Other', 'E2E', decode('01', 'hex'), decode('02', 'hex'),
            true, false, true, '${now}', 0, false, 0, '[]');
        INSERT INTO identity."UserRoles" ("Id", "UserId", "RoleId", "CreatedAt")
        SELECT gen_random_uuid(), users.user_id, roles."Id", '${now}'
          FROM (VALUES ('${studentId}'::uuid), ('${otherId}'::uuid)) AS users(user_id)
          CROSS JOIN identity."Roles" AS roles
          WHERE roles."Name"='Student' AND roles."IsDeleted"=false;
        INSERT INTO identity.student_profiles ("Id", "UserId", "InstitutionId", "FirstName", "LastName",
          "DailyGoalMinutes", "Preferences", "IsActive", "CreatedAt", "Version")
        VALUES
          (gen_random_uuid(), '${studentId}', '${ownInstitutionId}', 'Export', 'E2E',
            30, '{}', true, '${now}', 0),
          (gen_random_uuid(), '${otherId}', '${otherInstitutionId}', 'Other', 'E2E',
            30, '{}', true, '${now}', 0);
        COMMIT;
      `);
      seeded = true;
      sql(config, coachingDb, `BEGIN;
        INSERT INTO coaching.academic_goals
        (id, student_id, title, category, current_progress, is_completed, created_at, "Version")
        VALUES (gen_random_uuid(), '${studentId}', 'Own export goal', 'Other', 0, false, '${now}', 0),
          (gen_random_uuid(), '${otherId}', 'Other student secret', 'Other', 0, false, '${now}', 0);
        INSERT INTO coaching.coaching_sessions
          (id, teacher_id, institution_id, title, session_type, scheduled_date, duration_minutes, status,
            teacher_notes, teacher_notes_visibility, created_at, "Version")
        VALUES
          ('${privateSessionId}', '${teacherId}', '${ownInstitutionId}', 'Private-note session', 'OneOnOne', '${now}', 60,
            'Scheduled', 'Coach-only secret', 'CoachPrivate', '${now}', 0),
          ('${sharedSessionId}', '${teacherId}', '${ownInstitutionId}', 'Shared-note session', 'OneOnOne', '${now}', 60,
            'Scheduled', 'Shared guidance', 'StudentVisible', '${now}', 0),
          ('${otherSessionId}', '${teacherId}', '${otherInstitutionId}', 'Other student session', 'OneOnOne', '${now}', 60,
            'Scheduled', 'Other student private note', 'CoachPrivate', '${now}', 0);
        INSERT INTO coaching.session_attendances
          (id, session_id, student_id, attendance_status, student_note, teacher_note, created_at)
        VALUES
          (gen_random_uuid(), '${privateSessionId}', '${studentId}', 'NotRecorded',
            'My reflection', 'Attendance coach secret', '${now}'),
          (gen_random_uuid(), '${sharedSessionId}', '${studentId}', 'NotRecorded',
            null, null, '${now}'),
          (gen_random_uuid(), '${otherSessionId}', '${otherId}', 'NotRecorded',
            null, null, '${now}');
        INSERT INTO coaching.assignments
          (id, teacher_id, institution_id, title, type, source, due_date, status, created_at, "Version")
        VALUES
          ('${ownAssignmentId}', '${teacherId}', '${ownInstitutionId}', 'Own assignment', 'Individual', 'Digital',
            '${now}', 'Active', '${now}', 0),
          ('${otherAssignmentId}', '${teacherId}', '${otherInstitutionId}', 'Other assignment', 'Individual', 'Digital',
            '${now}', 'Active', '${now}', 0);
        INSERT INTO coaching.assignment_students
          (id, assignment_id, student_id, status, student_note, teacher_feedback, created_at)
        VALUES
          (gen_random_uuid(), '${ownAssignmentId}', '${studentId}', 'Assigned',
            'My assignment note', 'Teacher feedback for me', '${now}'),
          (gen_random_uuid(), '${otherAssignmentId}', '${otherId}', 'Assigned',
            'Other assignment secret', null, '${now}');
        INSERT INTO coaching.exams
          (id, created_by_teacher_id, institution_id, title, exam_type, exam_date, max_score, created_at, "Version")
        VALUES
          ('${ownExamId}', '${teacherId}', '${ownInstitutionId}', 'Own exam', 'Mock', '${now}', 100, '${now}', 0),
          ('${otherExamId}', '${teacherId}', '${otherInstitutionId}', 'Other exam', 'Mock', '${now}', 100, '${now}', 0);
        INSERT INTO coaching.exam_results
          (id, exam_id, student_id, score, teacher_notes, created_at)
        VALUES
          (gen_random_uuid(), '${ownExamId}', '${studentId}', 81,
            'Private exam teacher note', '${now}'),
          (gen_random_uuid(), '${otherExamId}', '${otherId}', 56,
            'Other exam secret', '${now}');
        COMMIT;`);

      assert.equal((await request(config, token(config, studentId, 'Teacher'))).status, 403);
      assert.equal((await request(config, token(config, unrelatedId, 'Student'))).status, 403);
      const response = await request(config, token(config, studentId, 'Student'));
      if (response.status !== 200) {
        assert.fail(`student export returned ${response.status}: ${await response.text()}`);
      }
      assert.match(response.headers.get('cache-control') || '', /no-store/);
      const exportData = await response.json();
      assert.equal(exportData.studentId.toLowerCase(), studentId);
      assert.deepEqual(exportData.goals.map(goal => goal.title), ['Own export goal']);
      assert.deepEqual(exportData.assignments.map(assignment => assignment.title), ['Own assignment']);
      assert.equal(exportData.assignments[0].studentNote, 'My assignment note');
      assert.equal(exportData.assignments[0].teacherFeedback, 'Teacher feedback for me');
      assert.deepEqual(exportData.exams.map(exam => exam.title), ['Own exam']);
      assert.equal(exportData.exams[0].score, 81);
      assert.deepEqual(exportData.agreements, []);
      assert.deepEqual(exportData.sessions.map(session => session.title).sort(),
        ['Private-note session', 'Shared-note session']);
      assert.equal(exportData.sessions.find(session => session.title === 'Private-note session').studentNote,
        'My reflection');
      assert.equal(exportData.sessions.find(session => session.title === 'Private-note session').sharedCoachNote,
        null);
      assert.equal(exportData.sessions.find(session => session.title === 'Shared-note session').sharedCoachNote,
        'Shared guidance');
      assert.doesNotMatch(JSON.stringify(exportData),
        /Coach-only secret|Attendance coach secret|Other student private note|Other assignment secret|Private exam teacher note|Other exam secret/);
      const otherResponse = await request(config, token(config, otherId, 'Student'));
      assert.equal(otherResponse.status, 200);
      const otherExport = await otherResponse.json();
      assert.equal(otherExport.studentId.toLowerCase(), otherId);
      assert.deepEqual(otherExport.goals.map(goal => goal.title), ['Other student secret']);
      assert.deepEqual(otherExport.assignments.map(assignment => assignment.title), ['Other assignment']);
      assert.deepEqual(otherExport.exams.map(exam => exam.title), ['Other exam']);
      assert.deepEqual(otherExport.sessions.map(session => session.title), ['Other student session']);
      assert.doesNotMatch(JSON.stringify(otherExport),
        /Own export goal|Own assignment|Own exam|Private-note session|Shared-note session/);

      identityStopped = true;
      command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'stop', 'identity-service']);
      const unavailable = await request(config, token(config, studentId, 'Student'));
      assert.ok(unavailable.status >= 500 && unavailable.status < 600,
        `Identity outage must fail closed, received ${unavailable.status}`);
      assert.doesNotMatch(await unavailable.text(), /Own export goal|Other student secret/);
      command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'start', 'identity-service']);
      identityStopped = false;
      await eventually(async () => (await request(config, token(config, studentId, 'Student'))).status === 200,
        'Identity recovery');

      postgresStopped = true;
      command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'stop', 'postgres']);
      const databaseUnavailable = await request(config, token(config, studentId, 'Student'), 35_000);
      assert.ok(databaseUnavailable.status >= 500 && databaseUnavailable.status < 600,
        `PostgreSQL outage must fail closed, received ${databaseUnavailable.status}`);
      assert.doesNotMatch(await databaseUnavailable.text(), /Own export goal|Other student secret/);
      command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'start', 'postgres']);
      postgresStopped = false;
      await eventually(() => sql(config, coachingDb, 'SELECT 1;') === '1', 'PostgreSQL recovery');
      await eventually(async () => (await request(config, token(config, studentId, 'Student'))).status === 200,
        'Coaching export recovery');

      rabbitStopped = true;
      command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'stop', 'rabbitmq']);
      const brokerUnavailable = await request(config, token(config, studentId, 'Student'));
      assert.equal(brokerUnavailable.status, 200,
        'Read-only Coaching export must remain available without RabbitMQ.');
      assert.deepEqual((await brokerUnavailable.json()).goals.map(goal => goal.title), ['Own export goal']);
      command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'start', 'rabbitmq']);
      rabbitStopped = false;

      sql(config, coachingDb, `BEGIN;
        INSERT INTO coaching.coaching_agreement_documents
          (id, document_version, locale, title, document_reference, content_sha256,
            published_by_user_id, effective_at, published_at, created_at, row_version)
        VALUES ('${agreementDocumentId}', 'e2e-${agreementDocumentId}', 'tr-TR', 'Export test agreement',
          'https://example.test/coaching/e2e', '${'a'.repeat(64)}', '${teacherId}',
          '${now}', '${now}', '${now}', 0);
        INSERT INTO coaching.coaching_agreement_acknowledgements
          (id, agreement_document_id, subject_student_id, acknowledged_by_user_id,
            party_role, acknowledged_at, created_at)
        VALUES
          ('${ownAcknowledgementId}', '${agreementDocumentId}', '${studentId}', '${studentId}',
            'Self', '${now}', '${now}'),
          ('${otherAcknowledgementId}', '${agreementDocumentId}', '${otherId}', '${otherId}',
            'Self', '${now}', '${now}');
        COMMIT;`);
      const withAgreement = await request(config, token(config, studentId, 'Student'));
      assert.equal(withAgreement.status, 200);
      const agreementExport = await withAgreement.json();
      assert.deepEqual(agreementExport.agreements.map(item => item.acknowledgementId.toLowerCase()),
        [ownAcknowledgementId]);
      assert.equal(agreementExport.agreements[0].documentVersion, `e2e-${agreementDocumentId}`);
      const otherAgreementResponse = await request(config, token(config, otherId, 'Student'));
      assert.equal(otherAgreementResponse.status, 200);
      const otherAgreementExport = await otherAgreementResponse.json();
      assert.deepEqual(otherAgreementExport.agreements.map(item => item.acknowledgementId.toLowerCase()),
        [otherAcknowledgementId]);
    } finally {
      if (rabbitStopped) {
        command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'start', 'rabbitmq']);
      }
      if (postgresStopped) {
        command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'start', 'postgres']);
        await eventually(() => sql(config, coachingDb, 'SELECT 1;') === '1', 'PostgreSQL cleanup readiness');
      }
      if (identityStopped) {
        command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'start', 'identity-service']);
      }
      if (seeded) {
        sql(config, coachingDb, `DELETE FROM coaching.coaching_agreement_acknowledgements
          WHERE id IN ('${ownAcknowledgementId}', '${otherAcknowledgementId}');
          DELETE FROM coaching.coaching_agreement_documents WHERE id='${agreementDocumentId}';`);
        sql(config, coachingDb, `DELETE FROM coaching.assignments
          WHERE id IN ('${ownAssignmentId}', '${otherAssignmentId}');
          DELETE FROM coaching.exams WHERE id IN ('${ownExamId}', '${otherExamId}');`);
        sql(config, coachingDb, `DELETE FROM coaching.coaching_sessions
          WHERE id IN ('${privateSessionId}', '${sharedSessionId}', '${otherSessionId}');`);
        sql(config, coachingDb, `DELETE FROM coaching.academic_goals WHERE student_id IN ('${studentId}', '${otherId}');`);
        sql(config, identityDb, `DELETE FROM identity.users WHERE "Id" IN ('${studentId}', '${otherId}');
          DELETE FROM identity.institutions WHERE "Id" IN ('${ownInstitutionId}', '${otherInstitutionId}');`);
      }
    }
  });
