import assert from 'node:assert/strict';
import { createHash, createHmac, randomUUID } from 'node:crypto';
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
  const result = spawnSync(docker, args, { cwd: repoRoot, input, encoding: 'utf8', timeout: 25_000 });
  if (result.error || result.status !== 0) {
    throw new Error(`Disposable attachment E2E Docker command failed: ${result.stderr?.trim() || result.error?.message}`);
  }
  return result.stdout.trim();
}

function verifyContainers(config) {
  requireDisposableEnvironment(config);
  assert.equal(config.ENVIRONMENT, 'Development');
  assert.match(config.COMPOSE_PROJECT_NAME || '', /^privacy-e2e-[a-z0-9-]+$/);
  for (const service of ['identity-service', 'coaching-service', 'api-gateway', 'postgres', 'minio']) {
    const id = command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'ps', '-q', service]);
    assert.ok(id, `${service} must run in the disposable Compose project`);
    const container = JSON.parse(command(['inspect', '--format', '{{json .}}', id]));
    assert.equal(container.Config.Labels['com.docker.compose.project'], config.COMPOSE_PROJECT_NAME);
    if (['identity-service', 'coaching-service', 'api-gateway'].includes(service)) {
      requireDisposableContainer(container.Config.Env, 'Development');
    }
    if (service === 'coaching-service') {
      assert.ok(container.Config.Env.includes('Coaching__Attachments__Provider=Minio'));
      assert.ok(container.Config.Env.includes(`Coaching__Attachments__MinioAccessKey=${config.ATTACHMENT_MINIO_ACCESS_KEY}`));
    }
  }
}

function sql(config, database, statement) {
  return command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'exec', '-T', 'postgres',
    'psql', '-X', '-v', 'ON_ERROR_STOP=1', '-U', config.POSTGRES_USER, '-d', database, '-At'],
  `${statement}\n`);
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

async function eventually(check, label) {
  const deadline = Date.now() + 60_000;
  while (Date.now() < deadline) {
    try {
      if (await check()) return;
    } catch {
      // Services and migrations may still be starting.
    }
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  throw new Error(`${label} did not become ready within 60 seconds.`);
}

async function expectStatus(response, status) {
  if (response.status !== status) {
    assert.fail(`Expected HTTP ${status}, got ${response.status}: ${await response.text()}`);
  }
}

test('disposable student attachment uses object storage and enforces ownership over HTTP',
  { timeout: 180_000 }, async () => {
    const config = await settings();
    verifyContainers(config);
    for (const key of ['POSTGRES_USER', 'JWT_SECRET', 'JWT_ISSUER', 'JWT_AUDIENCE',
      'GATEWAY_PORT', 'ATTACHMENT_MINIO_ACCESS_KEY']) assert.ok(config[key], `${key} is required`);
    const studentId = randomUUID();
    const otherId = randomUUID();
    const teacherId = randomUUID();
    const institutionId = randomUUID();
    const assignmentId = randomUUID();
    const agreementId = randomUUID();
    const now = new Date().toISOString();
    const identityDb = config.POSTGRES_DB_IDENTITY || 'identity_db';
    const coachingDb = config.POSTGRES_DB_COACHING || 'coaching_db';
    const bytes = Buffer.from([0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10, 0xff, 0xd9]);
    const sha256 = createHash('sha256').update(bytes).digest('hex').toUpperCase();
    const base = `http://127.0.0.1:${config.GATEWAY_PORT}/api/assignments/${assignmentId}/students/${studentId}/attachments`;
    const ownToken = token(config, studentId, 'Student');
    const otherToken = token(config, otherId, 'Student');
    const headers = bearer => ({ authorization: `Bearer ${bearer}` });

    await eventually(async () => (await fetch(`http://127.0.0.1:${config.GATEWAY_PORT}/health/live`)).ok,
      'Gateway');
    await eventually(() => sql(config, coachingDb,
      `SELECT to_regclass('coaching.assignment_submission_attachments') IS NOT NULL;`) === 't',
    'Coaching migration');
    await eventually(() => sql(config, identityDb,
      `SELECT count(*) FROM identity."Roles" WHERE "Name"='Student' AND "IsDeleted"=false;`) === '1',
    'Identity seed');

    sql(config, identityDb, `BEGIN;
      INSERT INTO identity.institutions
        ("Id", "Name", "Type", "LicenseType", "MaxStudents", "MaxTeachers", "IsActive", "CreatedAt", "Version")
      VALUES ('${institutionId}', 'Attachment E2E', 'School', 'Trial', 50, 5, true, '${now}', 0);
      INSERT INTO identity.users ("Id", "Email", "FirstName", "LastName", "PasswordHash", "PasswordSalt",
        "EmailConfirmed", "PhoneConfirmed", "IsActive", "CreatedAt", "Version", "MfaEnabled",
        "MfaFailedAttempts", "MfaRecoveryCodeHashesJson")
      VALUES
        ('${studentId}', 'attachment-${studentId}@example.test', 'Owner', 'E2E',
          decode('01', 'hex'), decode('02', 'hex'), true, false, true, '${now}', 0, false, 0, '[]'),
        ('${otherId}', 'attachment-${otherId}@example.test', 'Other', 'E2E',
          decode('01', 'hex'), decode('02', 'hex'), true, false, true, '${now}', 0, false, 0, '[]');
      INSERT INTO identity."UserRoles" ("Id", "UserId", "RoleId", "CreatedAt")
      SELECT gen_random_uuid(), users.user_id, roles."Id", '${now}'
        FROM (VALUES ('${studentId}'::uuid), ('${otherId}'::uuid)) AS users(user_id)
        CROSS JOIN identity."Roles" AS roles
        WHERE roles."Name"='Student' AND roles."IsDeleted"=false;
      INSERT INTO identity.student_profiles ("Id", "UserId", "InstitutionId", "FirstName", "LastName",
        "DailyGoalMinutes", "Preferences", "IsActive", "CreatedAt", "Version")
      VALUES
        (gen_random_uuid(), '${studentId}', '${institutionId}', 'Owner', 'E2E', 30, '{}', true, '${now}', 0),
        (gen_random_uuid(), '${otherId}', '${institutionId}', 'Other', 'E2E', 30, '{}', true, '${now}', 0);
      COMMIT;`);
    sql(config, coachingDb, `BEGIN;
      INSERT INTO coaching.assignments
        (id, teacher_id, institution_id, title, type, source, due_date, status, created_at, "Version")
      VALUES ('${assignmentId}', '${teacherId}', '${institutionId}', 'Attachment E2E', 'Individual',
        'Digital', '${now}', 'Active', '${now}', 0);
      INSERT INTO coaching.assignment_students (id, assignment_id, student_id, status, created_at)
      VALUES (gen_random_uuid(), '${assignmentId}', '${studentId}', 'Assigned', '${now}');
      INSERT INTO coaching.coaching_agreement_documents
        (id, document_version, locale, title, document_reference, content_sha256,
          published_by_user_id, effective_at, published_at, created_at, row_version)
      VALUES ('${agreementId}', 'e2e-${agreementId}', 'tr-TR', 'Attachment E2E agreement',
        'https://example.test/coaching/e2e', '${'a'.repeat(64)}', '${teacherId}',
        '${now}', '${now}', '${now}', 0);
      INSERT INTO coaching.coaching_agreement_acknowledgements
        (id, agreement_document_id, subject_student_id, acknowledged_by_user_id,
          party_role, acknowledged_at, created_at)
      VALUES (gen_random_uuid(), '${agreementId}', '${studentId}', '${studentId}', 'Self', '${now}', '${now}'),
        (gen_random_uuid(), '${agreementId}', '${otherId}', '${otherId}', 'Self', '${now}', '${now}');
      COMMIT;`);

    const create = await fetch(base, {
      method: 'POST', headers: { ...headers(ownToken), 'content-type': 'application/json' },
      body: JSON.stringify({ assignmentId, studentId, fileName: 'photo.jpg',
        contentType: 'image/jpeg', sizeBytes: bytes.length, sha256 }),
    });
    await expectStatus(create, 201);
    const attachment = await create.json();
    const contentUrl = `${base}/${attachment.attachmentId}/content`;
    assert.equal((await fetch(contentUrl, { headers: headers(ownToken) })).status, 409,
      'Pending attachment must not be downloadable');
    assert.equal((await fetch(contentUrl, { headers: headers(otherToken) })).status, 403,
      'Another student must not read the owner’s attachment');
    const upload = await fetch(contentUrl, {
      method: 'PUT', headers: { ...headers(ownToken), 'content-type': 'image/jpeg',
        'X-Content-SHA256': sha256 }, body: bytes,
    });
    await expectStatus(upload, 200);
    assert.equal((await upload.json()).status, 'Clean');
    const download = await fetch(contentUrl, { headers: headers(ownToken) });
    await expectStatus(download, 200);
    assert.deepEqual(Buffer.from(await download.arrayBuffer()), bytes);
    assert.equal((await fetch(contentUrl, { headers: headers(otherToken) })).status, 403);
    assert.equal(sql(config, coachingDb, `SELECT status FROM coaching.assignment_submission_attachments
      WHERE id='${attachment.attachmentId}';`), 'Clean');

    const pendingCreate = await fetch(base, {
      method: 'POST', headers: { ...headers(ownToken), 'content-type': 'application/json' },
      body: JSON.stringify({ assignmentId, studentId, fileName: 'pending.jpg',
        contentType: 'image/jpeg', sizeBytes: bytes.length, sha256 }),
    });
    await expectStatus(pendingCreate, 201);
    const pending = await pendingCreate.json();
    sql(config, identityDb, `UPDATE identity.users SET "IsActive"=false WHERE "Id"='${studentId}';`);

    const revokedUpload = await fetch(`${base}/${pending.attachmentId}/content`, {
      method: 'PUT', headers: { ...headers(ownToken), 'content-type': 'image/jpeg',
        'X-Content-SHA256': sha256 }, body: bytes,
    });
    await expectStatus(revokedUpload, 403);
    const revokedCreate = await fetch(base, {
      method: 'POST', headers: { ...headers(ownToken), 'content-type': 'application/json' },
      body: JSON.stringify({ assignmentId, studentId, fileName: 'revoked.jpg',
        contentType: 'image/jpeg', sizeBytes: bytes.length, sha256 }),
    });
    await expectStatus(revokedCreate, 403);
    assert.equal((await fetch(contentUrl, { headers: headers(ownToken) })).status, 403);
    assert.equal(sql(config, coachingDb, `SELECT status FROM coaching.assignment_submission_attachments
      WHERE id='${pending.attachmentId}';`), 'PendingUpload');
  });
