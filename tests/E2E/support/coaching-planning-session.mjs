// Disposable local session fixture, not an Identity login implementation.
import assert from 'node:assert/strict';
import { createHmac } from 'node:crypto';
import { createServer } from 'node:http';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

assert.equal(process.env.E2E_DISPOSABLE_ENV, 'true', 'This fixture is only for disposable local tests.');
export const planningStudent = 'dba472ec-a13e-453b-af7f-fec507c08817';
const key = 'local-planning-e2e-signing-key-20261002-never-use-in-production-123456789';
const internalKey = 'local-planning-e2e-internal-key-never-use-in-production-123456789';
export function planningToken(userId = planningStudent, roles = ['Student'], product = 'coaching') {
  const now = Math.floor(Date.now() / 1000);
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const body = `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({ sub: userId,
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': userId,
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': roles,
    platform_product: product, given_name: 'Planning', family_name: 'Test', email: 'planning@example.invalid',
    permission: roles.includes('SystemAdmin') ? ['Permissions.Coaching.View', 'Permissions.Coaching.ContentManage'] : [],
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
    if (request.url === '/api/internal/coaching/authorize-student-read' && request.method === 'POST') {
      if (request.headers['x-internal-service-key'] !== internalKey) { response.writeHead(403); response.end(); return; }
      try {
        const chunks = []; let length = 0;
        for await (const chunk of request) { length += chunk.length; if (length > 16_384) throw new Error('Body limit'); chunks.push(chunk); }
        const payload = JSON.parse(Buffer.concat(chunks).toString());
        assert.match(payload.viewerUserId, /^[0-9a-f-]{36}$/i);
        assert.ok(Array.isArray(payload.studentIds) && payload.studentIds.length <= 100);
        response.writeHead(200, { 'content-type': 'application/json' });
        response.end(JSON.stringify({ allowedStudentUserIds: payload.studentIds.filter(id => id === payload.viewerUserId) }));
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
