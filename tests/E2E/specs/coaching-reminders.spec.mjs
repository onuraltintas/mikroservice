import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { planningToken, planningStudent, planningTeacher, planningManager } from '../support/coaching-planning-session.mjs';

test('real coaching reminders reach only their recipients and student can retry marking one read', async ({ browser, request }) => {
  const base = 'http://127.0.0.1:5006'; const notifications = 'http://127.0.0.1:5012/api/notifications';
  const teacherHeaders = { Authorization: `Bearer ${planningToken(planningTeacher, ['Teacher'])}` };
  const studentHeaders = { Authorization: `Bearer ${planningToken(planningStudent)}` };
  const plans = `${base}/api/coaching/staff/students/${planningStudent}/study/plans`;
  const now = new Date();
  const date = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(now);
  expect(Number(new Intl.DateTimeFormat('en-GB', { timeZone: 'Europe/Istanbul', hour: '2-digit', hourCycle: 'h23' }).format(now))).toBeGreaterThanOrEqual(9);
  const context = await browser.newContext({ timezoneId: 'Europe/Istanbul' }); let plan; let sessionId;
  async function list(headers) {
    const response = await request.get(`${notifications}?pageNumber=1&pageSize=100`, { headers });
    expect(response.ok(), await response.text()).toBeTruthy(); return response.json();
  }
  try {
    const page = await context.newPage(); const received = [];
    await page.route('**/api/auth/refresh-token', route => route.fulfill({ json: { accessToken: planningToken(), tokenType: 'Bearer', expiresInMinutes: 15 } }));
    const connected = page.waitForEvent('websocket', { predicate: socket => socket.url().includes('/hubs/notifications') });
    page.on('websocket', socket => { if (socket.url().includes('/hubs/notifications')) socket.on('framereceived', frame => received.push(String(frame.payload))); });
    await page.goto('http://127.0.0.1:4300/coaching-portal/notifications'); await connected;
    const created = await request.post(plans, { headers: teacherHeaders, data: {
      title: `Hatırlatma testi ${randomUUID()}`, tasks: [{ plannedDate: date, title: 'Bugünkü çalışma', plannedMinutes: 30, topicId: null, isPinned: false }]
    } });
    expect(created.ok(), await created.text()).toBeTruthy(); plan = (await created.json()).data;
    const published = await request.post(`${plans}/${plan.id}/publish`, { headers: teacherHeaders, data: { expectedVersion: plan.version } });
    expect(published.ok(), await published.text()).toBeTruthy(); plan = (await published.json()).data;
    const scheduled = await request.post(`${base}/api/sessions`, { headers: { ...teacherHeaders, 'Idempotency-Key': randomUUID() }, data: {
      teacherId: planningTeacher, studentId: planningStudent, startTime: new Date(now.getTime() + 20 * 60_000).toISOString(),
      durationMinutes: 30, subject: `Hatırlatma seansı ${randomUUID()}`, type: 'OneOnOne', meetingLink: 'https://example.invalid/meeting'
    } });
    expect(scheduled.ok(), await scheduled.text()).toBeTruthy(); sessionId = (await scheduled.json()).sessionId;
    const ownReminder = item => item.type === 'StudyPlanReminder' && item.relatedEntityId === plan.id
      || item.type === 'SessionReminder' && item.relatedEntityId === sessionId;
    await expect.poll(async () => (await list(studentHeaders)).filter(ownReminder).length, { timeout: 75_000, intervals: [500, 1000, 2000] }).toBe(2);
    const own = (await list(studentHeaders)).filter(ownReminder);
    expect(new Set(own.map(item => item.id)).size).toBe(2);
    const teacher = (await list(teacherHeaders)).filter(ownReminder);
    expect(teacher).toHaveLength(1); expect(teacher[0].type).toBe('SessionReminder');
    expect((await list({ Authorization: `Bearer ${planningToken(planningManager, ['InstitutionAdmin'])}` })).filter(ownReminder)).toHaveLength(0);
    await expect.poll(() => received.filter(frame => frame.includes('ReceiveNotification')
      && (frame.includes('StudyPlanReminder') && frame.includes(plan.id) || frame.includes('SessionReminder') && frame.includes(sessionId))).length).toBe(2);
    const study = own.find(item => item.type === 'StudyPlanReminder');
    const card = page.locator(`article[data-notification-id="${study.id}"]`);
    await expect(card).toHaveCount(1); await expect(card.getByText('Yeni', { exact: true })).toBeVisible();
    let failOnce = true;
    await page.route('**/api/notifications/*/mark-as-read', async route => {
      if (failOnce) { failOnce = false; await route.fulfill({ status: 503, json: { message: 'Temporary test failure' } }); }
      else await route.continue();
    });
    await card.getByRole('button', { name: 'Okundu', exact: true }).click();
    await expect(page.getByRole('alert')).toContainText('Bildirim okunmuş olarak işaretlenemedi.');
    await expect(card.getByRole('button', { name: 'Okundu', exact: true })).toBeEnabled();
    await card.getByRole('button', { name: 'Okundu', exact: true }).click();
    await expect(card.getByText('Yeni', { exact: true })).toHaveCount(0);
    expect((await list(studentHeaders)).find(item => item.id === study.id).isRead).toBe(true);
    expect((await request.post(`${notifications}/${study.id}/mark-as-read`, { headers: teacherHeaders })).status()).toBe(404);
    await page.reload(); await expect(card).toHaveCount(1); await expect(card.getByText('Yeni', { exact: true })).toHaveCount(0);
    await page.setViewportSize({ width: 390, height: 844 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
    await page.screenshot({ path: '../../artifacts/coaching-completion-20261013/reminders-student-mobile.png', fullPage: true });
    expect((await list(studentHeaders)).filter(ownReminder).map(item => item.id).sort()).toEqual(own.map(item => item.id).sort());
  } finally {
    if (sessionId) expect((await request.post(`${base}/api/sessions/${sessionId}/cancel`, { headers: teacherHeaders })).ok()).toBeTruthy();
    if (plan) {
      const current = await request.get(`${plans}/${plan.id}`, { headers: teacherHeaders });
      if (current.ok()) {
        const value = (await current.json()).data;
        if (!value.tasks[0].isCompleted) expect((await request.put(`${base}/api/coaching/study-planning/plans/${plan.id}/tasks/${value.tasks[0].id}/completion`, {
          headers: studentHeaders, data: { expectedVersion: value.version, actualMinutes: 30 }
        })).ok()).toBeTruthy();
      }
    }
    await context.close();
  }
});
