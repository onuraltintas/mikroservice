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
  return { ...values, ...process.env, E2E_COMPOSE_ENV_FILE: file };
}

function command(args, input) {
  const result = spawnSync(docker, args, { cwd: repoRoot, input, encoding: 'utf8', timeout: 20_000 });
  if (result.error || result.status !== 0) {
    throw new Error(`Disposable admin E2E Docker command failed: ${result.stderr?.trim() || result.error?.message}`);
  }
  return result.stdout.trim();
}

function verifyContainers(config) {
  requireDisposableEnvironment(config);
  assert.equal(config.ENVIRONMENT, 'Development');
  assert.match(config.COMPOSE_PROJECT_NAME || '', /^privacy-e2e-[a-z0-9-]+$/);
  for (const service of ['identity-service', 'coaching-service', 'api-gateway', 'postgres']) {
    const id = command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'ps', '-q', service]);
    assert.ok(id, `${service} must be running in the disposable Compose project`);
    const container = JSON.parse(command(['inspect', '--format', '{{json .}}', id]));
    assert.equal(container.Config.Labels['com.docker.compose.project'], config.COMPOSE_PROJECT_NAME);
    if (service !== 'postgres') requireDisposableContainer(container.Config.Env, 'Development');
  }
}

function sql(config, database, statement) {
  return command(['compose', '--env-file', config.E2E_COMPOSE_ENV_FILE, 'exec', '-T', 'postgres',
    'psql', '-X', '-v', 'ON_ERROR_STOP=1', '-U', config.POSTGRES_USER, '-d', database, '-At'],
  `${statement}\n`);
}

function token(config, userId, role, admin = false) {
  const now = Math.floor(Date.now() / 1000);
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const unsigned = `${encode({ alg: 'HS256', typ: 'JWT', kid: config.JWT_KEY_ID })}.${encode({
    sub: userId, iss: config.JWT_ISSUER, aud: config.JWT_AUDIENCE,
    iat: now, nbf: now, exp: now + 300, [roleClaim]: role,
    ...(admin ? { permission: 'Permissions.Coaching.View', amr: 'mfa' } : {}),
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

test('disposable Coaching admin and teacher reads stay inside their Identity-backed scopes',
  { timeout: 180_000 }, async () => {
    const config = await settings();
    verifyContainers(config);
    for (const key of ['POSTGRES_USER', 'JWT_SECRET', 'JWT_ISSUER', 'JWT_AUDIENCE', 'GATEWAY_PORT']) {
      assert.ok(config[key], `${key} is required`);
    }
    const id = Object.fromEntries(['institutionA', 'institutionB', 'adminA', 'adminB',
      'teacherA', 'teacherB', 'studentA', 'studentB', 'assignmentA', 'assignmentB',
      'sessionA', 'sessionB', 'examA', 'examB', 'goalA', 'goalB']
      .map(name => [name, randomUUID()]));
    const now = new Date().toISOString();
    const identityDb = config.POSTGRES_DB_IDENTITY || 'identity_db';
    const coachingDb = config.POSTGRES_DB_COACHING || 'coaching_db';
    const base = `http://127.0.0.1:${config.GATEWAY_PORT}`;
    const auth = bearer => ({ authorization: `Bearer ${bearer}` });
    const get = (path, bearer) => fetch(`${base}${path}`, {
      headers: bearer ? auth(bearer) : {}, signal: AbortSignal.timeout(10_000),
    });

    await eventually(async () => (await get('/health/live')).ok, 'Gateway');
    await eventually(() => sql(config, identityDb,
      `SELECT count(*) FROM identity."Roles" WHERE "Name" IN ('Student', 'Teacher', 'InstitutionAdmin')
        AND "IsDeleted"=false;`) === '3', 'Identity roles');
    await eventually(() => sql(config, coachingDb,
      `SELECT to_regclass('coaching.assignments') IS NOT NULL;`) === 't', 'Coaching migration');

    sql(config, identityDb, `BEGIN;
      INSERT INTO identity.institutions
        ("Id", "Name", "Type", "LicenseType", "MaxStudents", "MaxTeachers", "IsActive", "CreatedAt", "Version")
      VALUES ('${id.institutionA}', 'Admin E2E A', 'School', 'Trial', 50, 5, true, '${now}', 0),
        ('${id.institutionB}', 'Admin E2E B', 'School', 'Trial', 50, 5, true, '${now}', 0);
      INSERT INTO identity.users ("Id", "Email", "FirstName", "LastName", "PasswordHash", "PasswordSalt",
        "EmailConfirmed", "PhoneConfirmed", "IsActive", "CreatedAt", "Version", "MfaEnabled",
        "MfaFailedAttempts", "MfaRecoveryCodeHashesJson")
      SELECT user_id, 'admin-scope-' || user_id || '@example.test', 'Scope', 'E2E',
        decode('01', 'hex'), decode('02', 'hex'), true, false, true, '${now}', 0, false, 0, '[]'
      FROM (VALUES ('${id.adminA}'::uuid), ('${id.adminB}'::uuid),
        ('${id.teacherA}'::uuid), ('${id.teacherB}'::uuid),
        ('${id.studentA}'::uuid), ('${id.studentB}'::uuid)) AS users(user_id);
      INSERT INTO identity."UserRoles" ("Id", "UserId", "RoleId", "CreatedAt")
      SELECT gen_random_uuid(), users.user_id, roles."Id", '${now}'
      FROM (VALUES
        ('${id.adminA}'::uuid, 'InstitutionAdmin'), ('${id.adminB}'::uuid, 'InstitutionAdmin'),
        ('${id.teacherA}'::uuid, 'Teacher'), ('${id.teacherB}'::uuid, 'Teacher'),
        ('${id.studentA}'::uuid, 'Student'), ('${id.studentB}'::uuid, 'Student'))
        AS users(user_id, role_name)
      JOIN identity."Roles" AS roles ON roles."Name"=users.role_name AND roles."IsDeleted"=false;
      INSERT INTO identity.institution_admins
        ("Id", "UserId", "InstitutionId", "Role", "IsActive", "CreatedAt")
      VALUES (gen_random_uuid(), '${id.adminA}', '${id.institutionA}', 'Admin', true, '${now}'),
        (gen_random_uuid(), '${id.adminB}', '${id.institutionB}', 'Admin', true, '${now}');
      INSERT INTO identity.student_profiles ("Id", "UserId", "InstitutionId", "FirstName", "LastName",
        "DailyGoalMinutes", "Preferences", "ShareProgressWithTeachers", "IsActive", "CreatedAt", "Version")
      VALUES (gen_random_uuid(), '${id.studentA}', '${id.institutionA}', 'Student', 'A',
        30, '{}', true, true, '${now}', 0),
        (gen_random_uuid(), '${id.studentB}', '${id.institutionB}', 'Student', 'B',
        30, '{}', true, true, '${now}', 0);
      INSERT INTO identity.teacher_profiles
        ("Id", "UserId", "InstitutionId", "FirstName", "LastName", subjects, "Certifications",
          "IsIndependent", "CanViewAllInstitutionStudents", "IsActive", "CreatedAt", "Version")
      VALUES (gen_random_uuid(), '${id.teacherA}', '${id.institutionA}', 'Teacher', 'A', '[]',
        '[]', false, false, true, '${now}', 0),
        (gen_random_uuid(), '${id.teacherB}', '${id.institutionB}', 'Teacher', 'B', '[]',
        '[]', false, false, true, '${now}', 0);
      INSERT INTO identity.teacher_student_assignments
        ("Id", "TeacherId", "StudentId", "InstitutionId", "StartDate", "IsActive", "CreatedAt")
      SELECT gen_random_uuid(), teacher."Id", student."Id", teacher."InstitutionId", '${now}', true, '${now}'
      FROM identity.teacher_profiles AS teacher
      JOIN identity.student_profiles AS student ON student."InstitutionId"=teacher."InstitutionId"
      WHERE teacher."UserId" IN ('${id.teacherA}', '${id.teacherB}')
        AND student."UserId" IN ('${id.studentA}', '${id.studentB}');
      COMMIT;`);
    sql(config, coachingDb, `BEGIN;
      INSERT INTO coaching.assignments
        (id, teacher_id, institution_id, title, type, source, due_date, status, created_at, "Version")
      VALUES ('${id.assignmentA}', '${id.teacherA}', '${id.institutionA}', 'Institution A only',
        'Individual', 'Digital', '${now}', 'Active', '${now}', 0),
        ('${id.assignmentB}', '${id.teacherB}', '${id.institutionB}', 'Institution B only',
        'Individual', 'Digital', '${now}', 'Active', '${now}', 0);
      INSERT INTO coaching.assignment_students (id, assignment_id, student_id, status, created_at)
      VALUES (gen_random_uuid(), '${id.assignmentA}', '${id.studentA}', 'Assigned', '${now}'),
        (gen_random_uuid(), '${id.assignmentB}', '${id.studentB}', 'Assigned', '${now}');
      INSERT INTO coaching.coaching_sessions
        (id, teacher_id, institution_id, title, session_type, scheduled_date, duration_minutes,
          status, teacher_notes, teacher_notes_visibility, created_at, "Version")
      VALUES ('${id.sessionA}', '${id.teacherA}', '${id.institutionA}', 'Session A only',
        'OneOnOne', '${now}', 60, 'Scheduled', 'Private note A', 'CoachPrivate', '${now}', 0),
        ('${id.sessionB}', '${id.teacherB}', '${id.institutionB}', 'Session B only',
        'OneOnOne', '${now}', 60, 'Scheduled', 'Private note B', 'CoachPrivate', '${now}', 0);
      INSERT INTO coaching.session_attendances
        (id, session_id, student_id, attendance_status, created_at)
      VALUES (gen_random_uuid(), '${id.sessionA}', '${id.studentA}', 'NotRecorded', '${now}'),
        (gen_random_uuid(), '${id.sessionB}', '${id.studentB}', 'NotRecorded', '${now}');
      INSERT INTO coaching.exams
        (id, created_by_teacher_id, institution_id, title, exam_type, exam_date, max_score, created_at, "Version")
      VALUES ('${id.examA}', '${id.teacherA}', '${id.institutionA}', 'Exam A only', 'Mock',
        '${now}', 100, '${now}', 0),
        ('${id.examB}', '${id.teacherB}', '${id.institutionB}', 'Exam B only', 'Mock',
        '${now}', 100, '${now}', 0);
      INSERT INTO coaching.exam_results (id, exam_id, student_id, score, created_at)
      VALUES (gen_random_uuid(), '${id.examA}', '${id.studentA}', 80, '${now}'),
        (gen_random_uuid(), '${id.examB}', '${id.studentB}', 60, '${now}');
      INSERT INTO coaching.academic_goals
        (id, student_id, title, category, current_progress, is_completed, created_at, "Version")
      VALUES ('${id.goalA}', '${id.studentA}', 'Goal A only', 'Other', 0, false, '${now}', 0),
        ('${id.goalB}', '${id.studentB}', 'Goal B only', 'Other', 0, false, '${now}', 0);
      COMMIT;`);

    const adminA = token(config, id.adminA, 'InstitutionAdmin', true);
    const adminB = token(config, id.adminB, 'InstitutionAdmin', true);
    const teacherA = token(config, id.teacherA, 'Teacher');
    const teacherB = token(config, id.teacherB, 'Teacher');
    await expectStatus(await get('/api/coaching-admin/assignments'), 401);
    await expectStatus(await get('/api/coaching-admin/assignments', teacherA), 403);
    const listA = await get('/api/coaching-admin/assignments', adminA);
    await expectStatus(listA, 200);
    assert.deepEqual((await listA.json()).items.map(item => item.id.toLowerCase()), [id.assignmentA]);
    const listB = await get('/api/coaching-admin/assignments', adminB);
    await expectStatus(listB, 200);
    assert.deepEqual((await listB.json()).items.map(item => item.id.toLowerCase()), [id.assignmentB]);
    await expectStatus(await get(`/api/coaching-admin/assignments/${id.assignmentA}`, adminA), 200);
    await expectStatus(await get(`/api/coaching-admin/assignments/${id.assignmentB}`, adminA), 404);
    await expectStatus(await get(`/api/coaching-admin/assignments/${id.assignmentB}`, adminB), 200);
    await expectStatus(await get(`/api/coaching-admin/assignments/${id.assignmentA}`, adminB), 404);
    for (const [resource, ownA, ownB] of [
      ['sessions', id.sessionA, id.sessionB],
      ['exams', id.examA, id.examB],
      ['goals', id.goalA, id.goalB],
    ]) {
      const a = await get(`/api/coaching-admin/${resource}`, adminA);
      await expectStatus(a, 200);
      assert.deepEqual((await a.json()).items.map(item => item.id.toLowerCase()), [ownA]);
      const b = await get(`/api/coaching-admin/${resource}`, adminB);
      await expectStatus(b, 200);
      assert.deepEqual((await b.json()).items.map(item => item.id.toLowerCase()), [ownB]);
      await expectStatus(await get(`/api/coaching-admin/${resource}/${ownA}`, adminA), 200);
      await expectStatus(await get(`/api/coaching-admin/${resource}/${ownB}`, adminA), 404);
      await expectStatus(await get(`/api/coaching-admin/${resource}/${ownB}`, adminB), 200);
      await expectStatus(await get(`/api/coaching-admin/${resource}/${ownA}`, adminB), 404);
    }
    for (const bearer of [adminA, adminB]) {
      const overview = await get('/api/coaching-admin/overview', bearer);
      await expectStatus(overview, 200);
      const counts = await overview.json();
      assert.equal(counts.totalAssignments, 1);
      assert.equal(counts.totalSessions, 1);
      assert.equal(counts.totalExams, 1);
      assert.equal(counts.totalGoals, 1);
    }

    const teacherList = await get(`/api/assignments/teacher/${id.teacherA}`, teacherA);
    await expectStatus(teacherList, 200);
    assert.deepEqual((await teacherList.json()).items.map(item => item.id.toLowerCase()), [id.assignmentA]);
    await expectStatus(await get(`/api/assignments/teacher/${id.teacherB}`, teacherA), 403);
    await expectStatus(await get(`/api/assignments/teacher/${id.teacherB}`, teacherB), 200);
    await expectStatus(await get(`/api/assignments/${id.assignmentA}`, teacherA), 200);
    await expectStatus(await get(`/api/assignments/${id.assignmentB}`, teacherA), 403);
    await expectStatus(await get(`/api/assignments/${id.assignmentB}`, teacherB), 200);

    sql(config, identityDb, `UPDATE identity.institution_admins SET "IsActive"=false
      WHERE "UserId"='${id.adminA}';`);
    await expectStatus(await get('/api/coaching-admin/assignments', adminA), 403);
  });
