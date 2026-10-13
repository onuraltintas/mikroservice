import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { planningToken, planningStudent, planningTeacher, planningManager, planningInstitution, planningIndependentTeacher, planningIndependentStudent, planningFinanceReminderStudent } from '../support/coaching-planning-session.mjs';

const base = 'http://127.0.0.1:5006/api/coaching/finance';
const headers = (user, role) => ({ Authorization: `Bearer ${planningToken(user, [role])}` });
const today = () => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
const localDate = value => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Istanbul', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date(value));
async function login(page, user, role, staff = false, studentId = planningStudent) {
  await page.route('**/api/auth/refresh-token', route => route.fulfill({ json: { accessToken: planningToken(user, [role]), tokenType: 'Bearer', expiresInMinutes: 15 } }));
  if (staff) {
    await page.route('**/api/auth/staff-session/products', route => route.fulfill({ json: [{ product: 'coaching', roles: [role] }] }));
    await page.route('**/api/teachers/me/students?*', route => route.fulfill({ json: { items: [{ userId: studentId, firstName: 'Ada', lastName: 'Test', fullName: 'Ada Test', gradeLevel: 8, assignmentStartDate: today() }], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 } }));
    await page.route(`**/api/coaching-admin/institutions/${planningInstitution}/teachers?*`, route => route.fulfill({ json: { teachers: [{ userId: planningTeacher, firstName: 'Test', lastName: 'Koç', email: 'coach@example.invalid' }], totalCount: 1 } }));
    await page.goto('http://127.0.0.1:4301/?product=coaching');
  }
}
async function cancelOwn(request, actor, studentId, feeId) {
  const root = `${base}/students/${studentId}/fees/${feeId}`;
  let response = await request.get(root, { headers: actor }); expect(response.ok(), await response.text()).toBeTruthy();
  let fee = (await response.json()).data;
  const history = await request.get(`${root}/payments`, { headers: actor }); expect(history.ok(), await history.text()).toBeTruthy();
  const payments = (await history.json()).data.items;
  for (const payment of payments.filter(item => item.amount > 0 && !payments.some(other => other.reversalOfPaymentId === item.id))) {
    response = await request.post(`${root}/payments/${payment.id}/reverse`, { headers: actor, data: { requestId: randomUUID(), expectedVersion: fee.version, receivedOn: today(), reason: 'Yerel test temizliği' } });
    expect(response.ok(), await response.text()).toBeTruthy(); fee = (await response.json()).data;
  }
  if (!fee.isCancelled) {
    response = await request.post(`${root}/cancel`, { headers: actor, data: { expectedVersion: fee.version, reason: 'Yerel test temizliği' } });
    expect(response.ok(), await response.text()).toBeTruthy();
  }
}

test('institution fee installments, safe retry, learner/admin read, scoped cash and immutable reversal work together', async ({ browser, request }) => {
  const manager = headers(planningManager, 'InstitutionAdmin'); const learner = headers(planningStudent, 'Student');
  const context = await browser.newContext({ timezoneId: 'Europe/Istanbul', viewport: { width: 390, height: 844 } });
  const learnerContext = await browser.newContext({ timezoneId: 'Europe/Istanbul', viewport: { width: 390, height: 844 } });
  const adminContext = await browser.newContext({ timezoneId: 'Europe/Istanbul' });
  let feeId;
  try {
    const page = await context.newPage(); await login(page, planningManager, 'InstitutionAdmin', true);
    await page.getByTestId('institution-students-tab').click(); await page.getByTestId(`student-report-${planningStudent}`).click();
    await page.getByRole('button', { name: 'Ücret ve tahsilat', exact: true }).click();
    const panel = page.locator('app-coaching-finance'); await panel.getByRole('button', { name: 'Ücret ve taksit oluştur' }).click();
    const title = `Ücret denemesi ${randomUUID()} ${'uzun başlık '.repeat(10)}`;
    const entry = panel.getByRole('form', { name: 'Manuel ücret işlemi' });
    await entry.getByLabel('Ücret başlığı').fill(title); await entry.getByLabel('Toplam ücret (₺)').fill('1000');
    await entry.getByLabel('1. taksit vadesi').fill('2026-10-01'); await entry.getByLabel('Tutar (₺)', { exact: true }).fill('400');
    await entry.getByRole('button', { name: 'Taksit ekle' }).click();
    await entry.getByLabel('2. taksit vadesi').fill('2030-11-01'); await entry.getByLabel('Tutar (₺)', { exact: true }).nth(1).fill('600');
    const created = page.waitForResponse(response => response.url().endsWith(`/students/${planningStudent}/fees`) && response.request().method() === 'POST');
    await entry.getByRole('button', { name: 'Kaydet', exact: true }).click(); const createResponse = await created;
    expect(createResponse.status(), await createResponse.text()).toBe(201); feeId = (await createResponse.json()).data.id;
    const feeCard = panel.locator(`[data-fee-id="${feeId}"]`); await expect(feeCard).toBeVisible(); await expect(feeCard).toContainText('01.10.2026');
    await feeCard.getByRole('button', { name: 'Tahsilat kaydet' }).click(); await entry.getByLabel('Tahsilat tutarı (₺)').fill('250');
    // Drop only the first response AFTER the real server committed, then retry the exact operation.
    const sentBodies = []; let first = true;
    await page.route(`**/api/coaching/finance/students/${planningStudent}/fees/${feeId}/payments`, async route => {
      if (route.request().method() !== 'POST') { await route.continue(); return; }
      sentBodies.push(route.request().postDataJSON()); const upstream = await route.fetch(); expect(upstream.ok(), await upstream.text()).toBeTruthy();
      if (first) { first = false; await route.fulfill({ status: 503, json: { message: 'Test: commit acknowledgement lost' } }); }
      else await route.fulfill({ response: upstream });
    });
    await entry.getByRole('button', { name: 'Kaydet', exact: true }).click(); await expect(panel.getByRole('alert')).toContainText('İşlem doğrulanamadı');
    await entry.getByRole('button', { name: 'Aynı işlemi yeniden dene' }).click(); await expect(entry).toHaveCount(0);
    expect(sentBodies).toHaveLength(2); expect(sentBodies[1]).toEqual(sentBodies[0]);
    await feeCard.getByRole('button', { name: 'Tahsilat geçmişi' }).click(); await expect(panel.locator('.receipt')).toHaveCount(1);
    const payment = panel.locator('.receipt'); const paymentId = await payment.getAttribute('data-payment-id');
    const data = await request.get(`${base}/me/fees/${feeId}`, { headers: learner }); expect(data.ok(), await data.text()).toBeTruthy();
    const view = (await data.json()).data; expect(view.paidAmount).toBe(250); expect(view.overdueAmount).toBe(150);
    expect((await request.post(`${base}/students/${planningStudent}/fees/${feeId}/payments`, { headers: learner, data: sentBodies[0] })).status()).toBe(403);
    expect((await request.get(`${base}/students/${planningStudent}/fees/${feeId}`, { headers: headers(planningTeacher, 'Teacher') })).status()).toBe(404);
    const learnerPage = await learnerContext.newPage(); await login(learnerPage, planningStudent, 'Student'); await learnerPage.goto('http://127.0.0.1:4300/coaching-portal/finance');
    const learnerPanel = learnerPage.locator('app-coaching-finance'); await expect(learnerPanel.locator(`[data-fee-id="${feeId}"]`)).toBeVisible();
    await expect(learnerPanel.locator('[data-pay-fee], [data-create-fee]')).toHaveCount(0);
    expect(Number(new Intl.DateTimeFormat('en-GB', { timeZone: 'Europe/Istanbul', hour: '2-digit', hourCycle: 'h23' }).format(new Date()))).toBeGreaterThanOrEqual(9);
    const notifications = 'http://127.0.0.1:5012/api/notifications?pageNumber=1&pageSize=100';
    const reminders = async actor => {
      const result = await request.get(notifications, { headers: actor }); expect(result.ok(), await result.text()).toBeTruthy();
      return (await result.json()).filter(item => item.type === 'FeeOverdueReminder' && item.relatedEntityId === planningStudent && localDate(item.createdAt) === today());
    };
    await expect.poll(async () => (await reminders(learner)).length, { timeout: 75_000, intervals: [500, 1000, 2000] }).toBe(1);
    const reminder = (await reminders(learner))[0]; expect(reminder.message).toContain('Vadesi geçen koçluk taksitin var.'); expect(reminder.message).not.toContain(title); expect(reminder.message).not.toMatch(/1000|250|150/);
    expect(await reminders(manager)).toHaveLength(0); expect(await reminders(headers(planningTeacher, 'Teacher'))).toHaveLength(0);
    const notificationPage = await learnerContext.newPage(); await notificationPage.goto('http://127.0.0.1:4300/coaching-portal/notifications');
    await expect(notificationPage.locator(`article[data-notification-id="${reminder.id}"]`)).toContainText('Koçluk ödeme hatırlatması');
    const adminPage = await adminContext.newPage(); await login(adminPage, randomUUID(), 'SystemAdmin'); await adminPage.goto('http://127.0.0.1:4300/dashboard/coaching/finance');
    const adminPanel = adminPage.locator('app-coaching-finance'); await adminPanel.locator(`[data-open-receivable="${feeId}"]`).click();
    await expect(adminPanel.locator(`[data-fee-id="${feeId}"]`)).toBeVisible(); await expect(adminPanel.locator('[data-pay-fee], [data-create-fee]')).toHaveCount(0);
    await payment.getByRole('button', { name: 'Tahsilatı düzelt' }).click(); await entry.getByLabel('Gerekçe').fill('Yanlış tahsilat kaydı'); await entry.getByRole('checkbox').check();
    await entry.getByRole('button', { name: 'Kaydet', exact: true }).click(); await expect(entry).toHaveCount(0); await expect(panel.locator('.receipt')).toHaveCount(2);
    await expect(panel.locator(`[data-payment-id="${paymentId}"]`)).toBeVisible(); await expect(panel.locator('.history')).toContainText('-₺250');
    await page.getByTestId('institution-finance-tab').click(); await expect(page.locator('app-coaching-finance').locator(`[data-open-receivable="${feeId}"]`)).toBeVisible();
    for (const visible of [page, learnerPage, notificationPage]) expect(await visible.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.screenshot({ path: '../../artifacts/coaching-completion-20261013/finance-institution-mobile.png', fullPage: true });
  } finally { if (feeId) await cancelOwn(request, manager, planningStudent, feeId); await Promise.all([context.close(), learnerContext.close(), adminContext.close()]); }
});

test('independent coach creates and settles only their own learner account', async ({ browser, request }) => {
  const coach = headers(planningIndependentTeacher, 'Teacher'); const context = await browser.newContext({ timezoneId: 'Europe/Istanbul' }); let feeId;
  try {
    const page = await context.newPage(); await login(page, planningIndependentTeacher, 'Teacher', true, planningIndependentStudent);
    await page.getByTestId('open-student-report').click(); await page.getByRole('button', { name: 'Ücret ve tahsilat', exact: true }).click();
    const panel = page.locator('app-coaching-finance'); await panel.getByRole('button', { name: 'Ücret ve taksit oluştur' }).click();
    const entry = panel.getByRole('form', { name: 'Manuel ücret işlemi' }); await entry.getByLabel('Ücret başlığı').fill(`Bağımsız koç ${randomUUID()}`);
    await entry.getByLabel('Toplam ücret (₺)').fill('200'); await entry.getByLabel('Tutar (₺)', { exact: true }).fill('200');
    const created = page.waitForResponse(response => response.url().endsWith(`/students/${planningIndependentStudent}/fees`) && response.request().method() === 'POST');
    await entry.getByRole('button', { name: 'Kaydet', exact: true }).click(); const response = await created;
    expect(response.status(), await response.text()).toBe(201); const fee = (await response.json()).data; feeId = fee.id; expect(fee.coachId).toBe(planningIndependentTeacher); expect(fee.institutionId).toBeNull();
    await panel.locator(`[data-fee-id="${feeId}"]`).getByRole('button', { name: 'Tahsilat kaydet' }).click(); await entry.getByLabel('Tahsilat tutarı (₺)').fill('200'); await entry.getByLabel('Yöntem').selectOption('BankTransfer');
    await entry.getByRole('button', { name: 'Kaydet', exact: true }).click(); await expect(entry).toHaveCount(0);
    expect((await request.get(`${base}/students/${planningIndependentStudent}/fees/${feeId}`, { headers: headers(planningTeacher, 'Teacher') })).status()).toBe(404);
    expect((await request.get(`${base}/students/${planningIndependentStudent}/fees/${feeId}`, { headers: headers(planningManager, 'InstitutionAdmin') })).status()).toBe(404);
    const own = await request.get(`${base}/me/fees/${feeId}`, { headers: headers(planningIndependentStudent, 'Student') }); expect(own.ok(), await own.text()).toBeTruthy(); expect((await own.json()).data.balance).toBe(0);
    const cash = await request.get(`${base}/cash?fromDate=${today()}&toDate=${today()}`, { headers: coach }); expect(cash.ok(), await cash.text()).toBeTruthy();
    expect((await cash.json()).data.items.some(item => item.agreementId === feeId && item.method === 'BankTransfer' && item.amount === 200)).toBe(true);
  } finally { if (feeId) await cancelOwn(request, coach, planningIndependentStudent, feeId); await context.close(); }
});

test('a separate learner receives the current-day overdue reminder, with same-day repeats deduplicated', async ({ browser, request }) => {
  expect(Number(new Intl.DateTimeFormat('en-GB', { timeZone: 'Europe/Istanbul', hour: '2-digit', hourCycle: 'h23' }).format(new Date()))).toBeGreaterThanOrEqual(9);
  const coach = headers(planningIndependentTeacher, 'Teacher'), learner = headers(planningFinanceReminderStudent, 'Student');
  const context = await browser.newContext({ timezoneId: 'Europe/Istanbul' }); let feeId;
  const list = async actor => {
    const response = await request.get('http://127.0.0.1:5012/api/notifications?pageNumber=1&pageSize=100', { headers: actor });
    expect(response.ok(), await response.text()).toBeTruthy();
    return (await response.json()).filter(item => item.type === 'FeeOverdueReminder' && item.relatedEntityId === planningFinanceReminderStudent && localDate(item.createdAt) === today());
  };
  try {
    const baseline = await list(learner); expect(baseline.length).toBeLessThanOrEqual(1);
    const evidence = baseline.length ? 'Existing current-day delivery; repeat checks deterministic deduplication.' : 'No current-day baseline; verifies fresh API-to-broker-to-notification delivery.';
    test.info().annotations.push({ type: 'reminder-evidence', description: evidence }); console.log(`Finance reminder evidence: ${evidence}`);
    const title = `Özel ücret ${randomUUID()}`;
    const created = await request.post(`${base}/students/${planningFinanceReminderStudent}/fees`, { headers: coach, data: { requestId: randomUUID(), title, totalAmount: 200, installments: [{ dueOn: '2026-10-01', amount: 200 }] } });
    expect(created.status(), await created.text()).toBe(201); feeId = (await created.json()).data.id;
    await expect.poll(async () => (await list(learner)).length, { timeout: 75_000, intervals: [500, 1000, 2000] }).toBe(1);
    const delivered = (await list(learner))[0]; expect(delivered.message).toContain('Vadesi geçen koçluk taksitin var.');
    expect(delivered.message).not.toContain(title); expect(delivered.message).not.toContain('200');
    if (baseline.length) expect(delivered.id).toBe(baseline[0].id);
    expect(await list(coach)).toHaveLength(0); expect(await list(headers(planningManager, 'InstitutionAdmin'))).toHaveLength(0);
    const page = await context.newPage(); await login(page, planningFinanceReminderStudent, 'Student'); await page.goto('http://127.0.0.1:4300/coaching-portal/notifications');
    await expect(page.locator(`article[data-notification-id="${delivered.id}"]`)).toContainText('Koçluk ödeme hatırlatması');
  } finally { if (feeId) await cancelOwn(request, coach, planningFinanceReminderStudent, feeId); await context.close(); }
});
