import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { extname, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';

// Roadmap D4: the built student coaching portal in a phone-sized browser with isolated API fixtures:
// an automated accessibility check (axe-core, WCAG 2.1 A/AA) and "student submits an assignment with a photo".
const root = fileURLToPath(new URL('../../', import.meta.url));
const dist = resolve(root, 'clients/admin-panel/dist/admin-panel/browser');
const axeSource = createRequire(import.meta.url).resolve('axe-core/axe.min.js');
const student = '3f2a1d63-0b7e-4c49-9a59-6b4a7c1f2e10';
const assignmentId = '7c1e5b2a-6f84-4d0e-9a3b-2c5d8e7f9a01';
const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.png': 'image/png', '.woff2': 'font/woff2', '.ico': 'image/x-icon', '.webmanifest': 'application/manifest+json', '.json': 'application/json' };

function token() {
  const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  return `${encode({ alg: 'HS256' })}.${encode({ sub: student, role: 'Student', platform_product: 'coaching', given_name: 'Ada', family_name: 'Öğrenci', email: 'ada@example.test', exp: Math.floor(Date.now() / 1000) + 900 })}.fixture`;
}

async function withStudentPage(action) {
  const server = createServer(async (request, response) => {
    const path = resolve(dist, '.' + decodeURIComponent(new URL(request.url, 'http://127.0.0.1').pathname));
    if (path !== dist && !path.startsWith(dist + sep)) { response.writeHead(403).end(); return; }
    const file = extname(path) ? path : resolve(dist, 'index.csr.html');
    try { const data = await readFile(file); response.writeHead(200, { 'content-type': types[extname(file)] ?? 'application/octet-stream' }); response.end(data); }
    catch { response.writeHead(404).end(); }
  });
  await new Promise(done => server.listen(0, '127.0.0.1', done));
  let browser;
  try {
    browser = await chromium.launch({ headless: true, ...(process.env.COACHING_SMOKE_CHROMIUM_EXECUTABLE_PATH ? { executablePath: process.env.COACHING_SMOKE_CHROMIUM_EXECUTABLE_PATH } : {}) });
    const page = await browser.newPage({ viewport: { width: 390, height: 844 }, locale: 'tr-TR', timezoneId: 'Europe/Istanbul' });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    page.setDefaultTimeout(15000);
    const api = await fixtures(page);
    await action(page, `http://127.0.0.1:${server.address().port}`, api);
    assert.deepEqual(errors, []);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true, 'phone layout must fit');
  } finally { await browser?.close(); await new Promise(done => server.close(done)); }
}

async function fixtures(page) {
  const calls = []; let status = 'Assigned'; const attachments = [];
  const assignment = () => ({
    id: assignmentId, teacherId: 'teacher-1', title: 'Paragraf çalışması', description: 'Sayfa 12–14 arasındaki soruları çöz.', subject: 'Türkçe',
    dueDate: new Date(Date.now() + 2 * 86400000).toISOString(), status: 'Active', maxScore: 100, source: 'Digital', estimatedDurationMinutes: 40,
    instructionAttachments: [], assignedStudents: [{ studentId: student, status, submittedAt: status === 'Submitted' ? new Date().toISOString() : null, score: null, teacherFeedback: null, studentNote: null, attachments }]
  });
  await page.route('**/api/**', async route => {
    const request = route.request(); const url = new URL(request.url()); const path = url.pathname; const method = request.method();
    calls.push(`${method} ${path}`);
    const json = body => route.fulfill({ json: body });
    if (path === '/api/auth/refresh-token') return json({ accessToken: token(), tokenType: 'Bearer', expiresInMinutes: 15 });
    if (path === `/api/assignments/${assignmentId}` && method === 'GET') return json(assignment());
    if (path === `/api/assignments/${assignmentId}/students/${student}/attachments` && method === 'POST') {
      const body = request.postDataJSON();
      assert.equal(body.contentType, 'image/jpeg'); assert.match(body.sha256, /^[0-9a-f]{64}$/);
      attachments.push({ id: 'attachment-1', originalFileName: body.fileName, contentType: body.contentType, sizeBytes: body.sizeBytes, status: 'PendingUpload' });
      return json({ attachmentId: 'attachment-1', uploadPath: `/api/assignments/${assignmentId}/students/${student}/attachments/attachment-1/content` });
    }
    if (path.endsWith('/attachments/attachment-1/content') && method === 'PUT') {
      assert.match(request.headers()['x-content-sha256'] ?? '', /^[0-9a-f]{64}$/);
      attachments[0].status = 'Clean'; return json({ attachmentId: 'attachment-1', status: 'Clean' });
    }
    if (path === `/api/assignments/${assignmentId}/submit` && method === 'POST') {
      assert.equal(request.postDataJSON().studentNote, 'Son soruda zorlandım.'); status = 'Submitted';
      return json({ message: 'Ödev teslim edildi.' });
    }
    if (path.includes('/notifications')) return json(path.endsWith('/unread-count') ? { count: 0 } : { items: [], totalCount: 0, pageNumber: 1, pageSize: 20 });
    if (path.startsWith('/api/assignments/student/')) return json({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25, totalPages: 0 });
    if (path.endsWith('/motivation')) return json({ studentId: student, currentStreak: 0, longestStreak: 0, activeToday: false, lastActiveDay: null, timeZoneId: 'Europe/Istanbul', badges: [], celebration: null });
    return json({ success: true, data: null });
  });
  return { calls };
}

async function axe(page, label) {
  await page.addScriptTag({ path: axeSource });
  const result = await page.evaluate(async () => (await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } }))
    .violations.filter(v => v.impact === 'serious' || v.impact === 'critical')
    .map(v => `${v.id} (${v.impact}): ${v.nodes.slice(0, 3).map(n => n.target.join(' ')).join(' | ')}`));
  assert.deepEqual(result, [], `${label}: accessibility violations`);
}

test('student assignment page passes the automated accessibility check and a photo can be submitted', async () => {
  await withStudentPage(async (page, base, api) => {
    await page.goto(`${base}/coaching-portal/assignments/${assignmentId}`);
    await page.getByRole('heading', { name: 'Paragraf çalışması' }).waitFor();
    await axe(page, 'assignment detail');

    const photo = Buffer.from('/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wAALCAABAAEBAREA/8QAFAABAAAAAAAAAAAAAAAAAAAACf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAD8AKp//2Q==', 'base64');
    await page.getByLabel('Fotoğraf çek').setInputFiles({ name: 'odev.jpg', mimeType: 'image/jpeg', buffer: photo });
    await page.getByText('Dosya yüklendi ve güvenlik taramasına alındı.').waitFor();
    await page.getByText('odev.jpg').first().waitFor();
    await page.getByLabel('Öğretmene notun').fill('Son soruda zorlandım.');
    await page.getByRole('button', { name: 'Ödevi teslim et' }).click();
    await page.getByText('Ödevin teslim edildi.', { exact: false }).waitFor();
    const writes = api.calls.filter(call => !call.startsWith('GET'));
    assert.deepEqual(writes.filter(call => !call.includes('refresh-token')), [
      `POST /api/assignments/${assignmentId}/students/${student}/attachments`,
      `PUT /api/assignments/${assignmentId}/students/${student}/attachments/attachment-1/content`,
      `POST /api/assignments/${assignmentId}/submit`
    ]);
    await axe(page, 'submitted assignment');
  });
});

test('student home passes the automated accessibility check', async () => {
  await withStudentPage(async (page, base) => {
    await page.goto(`${base}/coaching-portal`);
    await page.getByRole('navigation', { name: 'Hızlı erişim' }).waitFor();
    await axe(page, 'student home');
  });
});
