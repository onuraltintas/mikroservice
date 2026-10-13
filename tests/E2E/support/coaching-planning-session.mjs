// Disposable local session fixture, not an Identity login implementation.
import assert from 'node:assert/strict';
import { createHmac } from 'node:crypto';
import { createServer } from 'node:http';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

assert.equal(process.env.E2E_DISPOSABLE_ENV, 'true', 'This fixture is only for disposable local tests.');
export const planningStudent = 'dba472ec-a13e-453b-af7f-fec507c08817';
export const planningTeacher = '5ca5cb1c-b825-4d42-a378-0289b3f0a801';
export const planningManager = 'd7b42a6f-9284-435b-9e7e-40bef7f730f5';
export const planningInstitution = '9dc2d193-f024-4e86-b102-76a1c83165c7';
const key = 'local-planning-e2e-signing-key-20261002-never-use-in-production-123456789';
const internalKey = 'local-planning-e2e-internal-key-never-use-in-production-123456789';
export function planningToken(userId = planningStudent, roles = ['Student'], product = 'coaching') {
  const now = Math.floor(Date.now() / 1000);
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const body = `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({ sub: userId,
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': userId,
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': roles,
    platform_product: product, given_name: 'Planning', family_name: 'Test', email: 'planning@example.invalid',
    permission: roles.includes('SystemAdmin') ? ['Permissions.Coaching.View', 'Permissions.Coaching.ContentManage', 'Permissions.Coaching.Manage']
      : roles.includes('InstitutionAdmin') ? ['Permissions.Coaching.View'] : [],
    amr: roles.includes('SystemAdmin') ? ['mfa'] : [],
    iss: 'EduPlatform', aud: 'EduPlatform', iat: now, nbf: now, exp: now + 900 })}`;
  return `${body}.${createHmac('sha256', key).update(body).digest('base64url')}`;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  createServer(async (request, response) => {
    if (request.url === '/api/internal/mfa-policy/coaching' && request.method === 'GET') {
      if (request.headers['x-internal-service-key'] !== internalKey) { response.writeHead(403); response.end(); return; }
      response.writeHead(200, { 'content-type': 'application/json' });
      response.end(JSON.stringify({ mode: 'disabled' })); return;
    }
    // Only the external Identity ownership contract is stubbed; Coaching queries remain real.
    if (['/api/internal/coaching/authorize', '/api/internal/coaching/authorize-admin', '/api/internal/coaching/report-students', '/api/internal/coaching/report-student-page'].includes(request.url)
        && request.method === 'POST') {
      if (request.headers['x-internal-service-key'] !== internalKey) { response.writeHead(403); response.end(); return; }
      try {
        const chunks = []; let length = 0;
        for await (const chunk of request) { length += chunk.length; if (length > 16_384) throw new Error('Body limit'); chunks.push(chunk); }
        const payload = JSON.parse(Buffer.concat(chunks).toString());
        let result;
        if (request.url.endsWith('/authorize')) {
          assert.equal(payload.teacherId, planningTeacher);
          assert.ok(Array.isArray(payload.studentIds) && payload.studentIds.length === 1 && payload.studentIds[0] === planningStudent);
          assert.equal(payload.isSystemAdministrator, false);
          result = { institutionId: planningInstitution };
        } else {
          assert.equal(payload.viewerUserId, planningManager);
          if (request.url.endsWith('/report-students') || request.url.endsWith('/report-student-page')) {
            assert.equal(payload.institutionId, planningInstitution);
            const matches = (payload.gradeLevel == null || payload.gradeLevel === 8)
              && (payload.teacherUserId == null || payload.teacherUserId === planningTeacher)
              && (payload.search == null || 'ada test'.includes(payload.search.toLowerCase()));
            result = { studentUserIds: matches ? [planningStudent] : [] };
            if (request.url.endsWith('/report-student-page')) {
              assert.ok(Number.isInteger(payload.pageNumber) && payload.pageNumber >= 1 && payload.pageNumber <= 1000);
              assert.ok(Number.isInteger(payload.pageSize) && payload.pageSize >= 1 && payload.pageSize <= 100);
              const students = matches && payload.pageNumber === 1 ? [{ userId: planningStudent, firstName: 'Ada', lastName: 'Test',
                email: 'ada@example.invalid', gradeLevel: 8, teacherName: 'Test Koç', teacherUserId: planningTeacher }] : [];
              result = { studentUserIds: students.map(student => student.userId), students, totalCount: matches ? 1 : 0 };
            }
          } else result = { isGlobal: false, institutionId: planningInstitution };
        }
        response.writeHead(200, { 'content-type': 'application/json' }); response.end(JSON.stringify(result));
      } catch { response.writeHead(403); response.end(); }
      return;
    }
    if (request.url === '/api/internal/coaching/authorize-student-read' && request.method === 'POST') {
      if (request.headers['x-internal-service-key'] !== internalKey) { response.writeHead(403); response.end(); return; }
      try {
        const chunks = []; let length = 0;
        for await (const chunk of request) { length += chunk.length; if (length > 16_384) throw new Error('Body limit'); chunks.push(chunk); }
        const payload = JSON.parse(Buffer.concat(chunks).toString());
        assert.match(payload.viewerUserId, /^[0-9a-f-]{36}$/i);
        assert.ok(Array.isArray(payload.studentIds) && payload.studentIds.length <= 100);
        response.writeHead(200, { 'content-type': 'application/json' });
        response.end(JSON.stringify({ allowedStudentUserIds: payload.studentIds.filter(id => id === payload.viewerUserId
          || id === planningStudent && [planningTeacher, planningManager].includes(payload.viewerUserId)) }));
      } catch { response.writeHead(400); response.end(); }
      return;
    }
    if (request.url === '/api/auth/refresh-token' && request.method === 'POST') {
      response.writeHead(200, { 'content-type': 'application/json' });
      response.end(JSON.stringify({ accessToken: planningToken(), tokenType: 'Bearer', expiresInMinutes: 15 }));
      return;
    }
    if (!request.url || !/^\/api\/(coaching\/|coaching-admin\/|goals(?:\/|\?|$)|reports\/|exams\/)/.test(request.url)) {
      response.writeHead(404); response.end(); return;
    }
    try {
      const chunks = []; let length = 0;
      for await (const chunk of request) {
        length += chunk.length;
        if (length > 48 * 1024 * 1024) { response.writeHead(413); response.end(); return; }
        chunks.push(chunk);
      }
      const headers = { ...request.headers }; delete headers.host; delete headers.connection;
      const upstream = await fetch(`http://127.0.0.1:5006${request.url}`, {
        method: request.method, headers, body: chunks.length ? Buffer.concat(chunks) : undefined,
        signal: AbortSignal.timeout(15_000)
      });
      response.writeHead(upstream.status, { 'content-type': upstream.headers.get('content-type') ?? 'application/json' });
      response.end(Buffer.from(await upstream.arrayBuffer()));
    } catch { response.writeHead(502); response.end('Local Coaching test API unavailable.'); }
  }).listen(4600, '127.0.0.1', () => console.log('Disposable planning session fixture: 127.0.0.1:4600'));
}
