import { test, expect } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { planningToken } from '../support/coaching-planning-session.mjs';
import { studentShell } from '../support/coaching-student-shell.mjs';

test('real API enforces authentication, product scope, role and student ownership', async ({ request }) => {
  const url = 'http://127.0.0.1:5006/api/coaching/study-planning/plans';
  expect((await request.get(url)).status()).toBe(401);
  expect((await request.get(url, { headers: { Authorization: `Bearer ${planningToken(randomUUID(), ['Teacher'])}` } })).status()).toBe(403);
  expect((await request.get(url, { headers: { Authorization: `Bearer ${planningToken(randomUUID(), ['Student'], 'speed-reading')}` } })).status()).toBe(403);
  const owner = planningToken(randomUUID()); const headers = { Authorization: `Bearer ${owner}` };
  const created = await request.post(url, { headers, data: { title: 'Ownership test', tasks: [
    { plannedDate: '2026-10-02', title: 'Review', plannedMinutes: 30, topicId: null, isPinned: false }
  ] } });
  expect(created.ok(), await created.text()).toBeTruthy();
  const id = (await created.json()).data.id;
  const other = await request.get(`${url}/${id}`, { headers: { Authorization: `Bearer ${planningToken(randomUUID())}` } });
  expect(other.status()).toBe(404);
});

test('student creates, publishes, completes a plan and enters a self-reported exam', async ({ page }) => {
  const student = randomUUID();
  await page.route('**/api/auth/refresh-token', route => route.fulfill({ status: 200, contentType: 'application/json',
    body: JSON.stringify({ accessToken: planningToken(student), tokenType: 'Bearer', expiresInMinutes: 15 }) }));
  const headers = { Authorization: `Bearer ${planningToken(student)}`, 'Idempotency-Key': randomUUID() };
  const goalTitle = `Browser goal ${randomUUID()}`;
  const goalResponse = await page.request.post('http://127.0.0.1:5006/api/goals', { headers, data: {
    studentId: student, title: goalTitle, category: 'ExamPreparation', teacherId: null, description: null,
    targetDate: '2027-06-01T00:00:00Z', targetScore: 400
  } });
  expect(goalResponse.ok(), await goalResponse.text()).toBeTruthy();
  const goalId = (await goalResponse.json()).goalId;
  expect((await page.request.put(`http://127.0.0.1:5006/api/goals/${goalId}/progress`, {
    headers, data: { goalId, progress: 30 }
  })).ok()).toBeTruthy();
  // E7 (#30): Hedefim no longer offers a free exam type and scale to the student (the wizard's target step sets the scale from the
  // score type); this report check keeps the goal-target score API with a practice-exam scale.
  const auth = { Authorization: `Bearer ${planningToken(student)}` };
  const goalTarget = `http://127.0.0.1:5006/api/coaching/study-planning/goals/${goalId}/target`;
  const version = (await (await page.request.get(goalTarget, { headers: auth })).json()).data.version;
  const scored = await page.request.put(`${goalTarget}/score`, { headers: auth, data: { expectedVersion: version, targetScore: 90, maxScore: 100, examType: 'Mock' } });
  expect(scored.ok(), await scored.text()).toBeTruthy();
  await page.goto('/coaching-portal/plan');
  await expect(page.getByRole('heading', { name: 'Çalışma planlarım', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Yeni taslak', exact: true }).click();
  const title = `Browser plan ${randomUUID()}`;
  await page.getByLabel('Plan başlığı', { exact: true }).fill(title);
  await page.getByLabel('Çalışma başlığı', { exact: true }).fill('Review fractions');
  await page.getByLabel('Tarih', { exact: true }).fill('2026-10-02');
  await page.getByLabel('Süre (dk)', { exact: true }).fill('30');
  await page.getByRole('button', { name: 'Taslağı kaydet', exact: true }).click();
  await expect(page.getByLabel('Planımı kontrol ettim, yayımlamayı onaylıyorum.')).toBeEnabled();
  await page.getByLabel('Planımı kontrol ettim, yayımlamayı onaylıyorum.').check();
  await page.getByRole('button', { name: 'Planı yayımla', exact: true }).click();
  await expect(page.getByLabel('Gerçek süre (dk)', { exact: true })).toBeVisible();
  await page.getByLabel('Gerçek süre (dk)', { exact: true }).fill('25');
  await page.getByRole('button', { name: 'Tamamla', exact: true }).click();
  await expect(page.getByText(/Tamamlandı · 25 dk/)).toBeVisible();
  await page.goto('/coaching-portal/growth/exams');
  await page.getByLabel('Başlık', { exact: true }).fill(`Browser exam ${randomUUID()}`);
  await page.getByLabel('Tarih', { exact: true }).fill('2026-10-02');
  await page.getByLabel('Aldığın puan', { exact: true }).fill('80');
  await page.getByLabel('Doğru', { exact: true }).fill('8');
  await page.getByLabel('Yanlış', { exact: true }).fill('1');
  await page.getByLabel('Boş', { exact: true }).fill('1');
  await page.getByLabel('Bilgilerimi kontrol ettim; öğrenci beyanı olarak kaydet.').check();
  await page.getByRole('button', { name: 'Sonucu kaydet', exact: true }).click();
  await expect(page.getByText('Sonucun kaydedildi. Öğrenci beyanı olarak raporlanacak.')).toBeVisible();
  const report = await page.request.get('http://127.0.0.1:5006/api/coaching/study-planning/reports?fromDate=2026-10-02&toDate=2026-10-02', {
    headers: { Authorization: `Bearer ${planningToken(student)}` }
  });
  expect(report.ok()).toBeTruthy();
  const data = (await report.json()).data;
  expect(data.completedTasks).toBe(1); expect(data.actualMinutes).toBe(25);
  expect(data.examGroups.some(x => x.source === 'StudentReported' && x.averagePercentage === 80)).toBeTruthy();
  expect(data.goals).toHaveLength(1);
  expect(data.goals[0]).toMatchObject({ goalId, recordedProgress: 30, source: 'Unspecified' });
  expect(data.goals[0].scoreAssessment.comparisons[0]).toMatchObject({ source: 'StudentReported', targetAttainmentPercentage: 88.9, remainingScore: 10 });
  await page.goto('/coaching-portal/growth/week');
  const studyReport = page.locator('app-student-study-report');
  await studyReport.getByLabel('Başlangıç', { exact: true }).fill('2026-10-02');
  await studyReport.getByLabel('Bitiş', { exact: true }).fill('2026-10-02');
  await studyReport.getByRole('button', { name: 'Raporu göster', exact: true }).click();
  await expect(studyReport.getByText('Tamamlanan çalışma', { exact: true })).toBeVisible();
  await expect(studyReport.getByText(/Ortalama %80/)).toBeVisible();
  await expect(studyReport.getByText(goalTitle, { exact: true })).toBeVisible();
  await expect(studyReport.getByText(/Kaydedilen ilerleme: %30/)).toBeVisible();
  await expect(studyReport.getByText(/Puan hedefine erişim: %88.9/)).toBeVisible();
});

test('student links a school target and publishes a plan through the wizard within saved capacity', async ({ page }) => {
  const student = randomUUID();
  await page.route('**/api/auth/refresh-token', route => route.fulfill({ status: 200, contentType: 'application/json',
    body: JSON.stringify({ accessToken: planningToken(student), tokenType: 'Bearer', expiresInMinutes: 15 }) }));
  const headers = { Authorization: `Bearer ${planningToken(student)}`, 'Idempotency-Key': randomUUID() };
  const created = await page.request.post('http://127.0.0.1:5006/api/goals', { headers, data: {
    studentId: student, title: `Catalog goal ${randomUUID()}`, category: 'ExamPreparation', teacherId: null,
    description: null, targetDate: '2027-06-01T00:00:00Z', targetScore: null
  } });
  expect(created.ok(), await created.text()).toBeTruthy();
  const goalId = (await created.json()).goalId;
  // E7 (#30): the student picks a school in Hedefim through the wizard's target step; here the catalog search (case-folded) and the
  // goal-target API link it.
  const auth = { Authorization: `Bearer ${planningToken(student)}` };
  const found = await page.request.get('http://127.0.0.1:5006/api/coaching/study-planning/targets/admissions?examCode=LGS&year=2026&search=e2e%20okul', { headers: auth });
  expect(found.ok(), await found.text()).toBeTruthy();
  const school = (await found.json()).data.items.find(x => x.name === 'E2E Okul');
  expect(school).toBeTruthy();
  const goalTarget = `http://127.0.0.1:5006/api/coaching/study-planning/goals/${goalId}/target`;
  const version = (await (await page.request.get(goalTarget, { headers: auth })).json()).data.version;
  const linked = await page.request.put(goalTarget, { headers: auth, data: { expectedVersion: version, targetUniversityProgramId: null, targetSchoolId: school.targetId } });
  expect(linked.ok(), await linked.text()).toBeTruthy();
  const target = await page.request.get(`http://127.0.0.1:5006/api/coaching/study-planning/goals/${goalId}/target`, { headers });
  expect((await target.json()).data.catalogTarget.name).toBe('E2E Okul');
  await page.goto('/coaching-portal/plan/hours');
  await page.getByRole('button', { name: 'Saat aralığı ekle', exact: true }).click();
  await page.getByRole('combobox', { name: 'Gün', exact: true }).selectOption('Friday');
  await page.getByLabel('Başlangıç', { exact: true }).fill('09:00');
  await page.getByLabel('Bitiş', { exact: true }).fill('10:00');
  await page.getByRole('button', { name: 'Çalışma saatlerini kaydet', exact: true }).click();
  // F2 (#27): no "Yeniden yükle" after a successful load; the save confirms itself.
  await expect(page.getByText('Çalışma saatlerin kaydedildi.', { exact: false }).first()).toBeVisible();
  // Review of PR 135 (S16): the plan is made through the wizard, as a student does: the saved hours, the courses, the tempo, the
  // summary, then "Planımı yayımla". The shell calls (first-login state, navigation, unread count) come from the shared helper.
  await page.route('**/api/**', route => studentShell(route, new URL(route.request().url()).pathname) ?? route.fallback());
  const api = 'http://127.0.0.1:5006/api/coaching/study-planning';
  const topics = await page.request.get(`${api}/topics?search=${encodeURIComponent('E2E Kesirler')}&gradeNumber=8&pageNumber=1&pageSize=20`, { headers: auth });
  expect(topics.ok(), await topics.text()).toBeTruthy();
  const topic = (await topics.json()).data.items.find(x => x.name === 'E2E Kesirler');
  expect(topic?.lessonId, 'the seeded E2E Kesirler topic names its lesson').toBeTruthy();
  // The wizard plans the chosen courses: choose the topic's lesson (as Çalışma tercihlerim does).
  const setupSaved = await page.request.put(`${api}/setup`, { headers: auth, data: { expectedVersion: null, academicYearStart: 2026, purpose: 'Lgs', targetExamYear: 2027,
    primaryScoreType: null, foreignLanguage: null, targetExamSessionId: null, courses: [{ lessonId: topic.lessonId, priority: 3, selfReportedDifficulty: null }], topics: [],
    startingStatusReviewed: true } });
  expect(setupSaved.ok(), await setupSaved.text()).toBeTruthy();
  await page.goto('/coaching-portal/plan/wizard');
  await expect(page.getByRole('heading', { name: 'Saatlerin', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Bu saatlerle devam', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Derslerin', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Devam', exact: true }).click();
  // The tempo step is skipped when only one tempo fits; otherwise the preselected one is kept.
  const summary = page.getByRole('heading', { name: 'Özet', exact: true });
  await expect(summary.or(page.getByRole('heading', { name: 'Tempo', exact: true }))).toBeVisible();
  if (!await summary.isVisible()) await page.getByRole('button', { name: 'Devam', exact: true }).click();
  await expect(summary).toBeVisible();
  await page.getByRole('button', { name: 'Bu haftayla devam et', exact: true }).click();
  await expect(page.getByTestId('course-finish')).toBeVisible();
  await page.getByRole('button', { name: 'Planımı yayımla', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Yayımla', exact: true }).click();
  await expect(page).toHaveURL(/\/coaching-portal\/plan\?plan=/);
  const plans = await page.request.get(`${api}/plans?status=Active&pageNumber=1&pageSize=20`, { headers: auth });
  const active = (await plans.json()).data.items;
  expect(active).toHaveLength(1);
  const plan = (await (await page.request.get(`${api}/plans/${active[0].id}`, { headers: auth })).json()).data;
  expect(plan.tasks.length).toBeGreaterThan(0);
  const first = plan.tasks.map(x => x.plannedDate).sort()[0];
  const report = await page.request.get(`${api}/reports?fromDate=${first}&toDate=${first}`, { headers });
  const data = (await report.json()).data;
  expect(data.scheduledTasks).toBeGreaterThan(0); expect(data.completedTasks).toBe(0);
  expect(data.goals[0].scoreAssessment).toMatchObject({ reason: 'ScoreTargetNotConfigured', comparisons: [] });
});
