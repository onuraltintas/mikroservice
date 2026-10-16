import { test, expect } from '@playwright/test';

// These browser journeys exercise the production UI with isolated API fixtures.
// Real persistence, authorization and migration are covered by PostgreSQL / API tests.
const student = 'b8a09753-f3ea-44d8-a444-cf49577b4a01';
const token = () => {
  const encode = x => Buffer.from(JSON.stringify(x)).toString('base64url');
  return `${encode({ alg: 'HS256' })}.${encode({ sub: student, role: 'Student', platform_product: 'coaching', given_name: 'Test', family_name: 'Öğrenci', exp: Math.floor(Date.now() / 1000) + 900 })}.fixture`;
};
async function fixtures(context) {
  let profile = { version: 0, educationStatus: 'Unknown', gradeLevel: null, isActive: true, shareProgressWithTeachers: true };
  let learning = null; let hours = null; const previews = [];
  const next = () => profile.educationStatus.toLowerCase() === 'unknown' ? 'Profile' : !learning ? 'Purpose'
    : !hours?.windows.length ? 'Availability' : !learning.coursePreferences.length ? 'Courses' : !learning.startingStatusReviewed ? 'Starting' : 'Preview';
  const view = () => ({ context: learning, nextStep: next(), isReady: next() === 'Preview', missingFields: next() === 'Preview' ? [] : [next()], warnings: [] });
  const lesson = { id: 'lesson-1', name: 'Matematik', gradeNumber: 12, examCode: null, topicCount: 1, missingDurationCount: 0 };
  await context.route('**/api/**', async route => {
    const request = route.request(); const url = new URL(request.url()); const path = url.pathname;
    const respond = data => route.fulfill({ json: { success: true, data } });
    if (path === '/api/auth/refresh-token') return route.fulfill({ json: { accessToken: token(), tokenType: 'Bearer', expiresInMinutes: 15 } });
    if (path.endsWith('/profiles/me/student')) { profile = { ...profile, ...request.postDataJSON(), version: profile.version + 1 }; return respond(profile); }
    if (path.endsWith('/profiles/me')) return respond({ student: profile, teacher: null, memberships: [] });
    if (path.endsWith('/setup/catalog')) return respond({ curriculum: { id: 'curriculum', name: 'Test ders programı', versionLabel: 'v1' }, lessons: [lesson],
      topics: { items: url.searchParams.get('lessonId') ? [{ id: 'topic-1', lessonId: lesson.id, name: 'Doğal sayılar', lessonName: 'Matematik', unitName: 'Sayılar', estimatedMinutes: 30, gradeNumber: 12, examCode: null }] : [], totalCount: 1, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] });
    if (path.endsWith('/setup')) {
      if (request.method() === 'PUT') { const r = request.postDataJSON(); learning = { ...r, id: 'context', version: (learning?.version ?? -1) + 1, coursePreferences: r.courses, topicStates: r.topics, curriculumVersionId: 'curriculum' }; }
      return respond(view());
    }
    if (path.endsWith('/availability')) { if (request.method() === 'PUT') hours = { ...request.postDataJSON(), version: (hours?.version ?? -1) + 1 }; return respond(hours); }
    if (path.endsWith('/automatic-preview')) {
      const r = request.postDataJSON(); previews.push(r);
      return respond({ availabilityVersion: hours.version, timeZoneId: 'Europe/Istanbul', activeRevisionId: 'existing', activeRevisionVersion: 1, protectedTasks: [],
        schedule: { tasks: [{ topicId: 'topic-1', plannedDate: r.startDate, plannedMinutes: 30 }], unscheduledTopics: [], availableMinutes: 60, scheduledMinutes: 30, unscheduledMinutes: 0, unusedMinutes: 30 } });
    }
    if (path.endsWith('/plans/existing')) return respond({ id: 'existing', version: 1, title: 'Mevcut koç planı', status: 'Active', tasks: [{ id: 'old-task', title: 'Önceki çalışma', plannedDate: '2026-10-19', plannedMinutes: 30, isPinned: false, isCompleted: true, actualMinutes: 25 }] });
    if (path.endsWith('/plans')) return respond({ items: [{ id: 'existing', version: 1, title: 'Mevcut koç planı', status: 'Active' }], totalCount: 1, pageNumber: 1, pageSize: 20 });
    if (path.includes('assignments')) return respond({ items: [], totalCount: 0, pageNumber: 1, pageSize: 5 });
    return respond([]);
  });
  return { previews };
}

test('student resumes setup after reload and previews a plan from selected courses', async ({ page, context }) => {
  const { previews } = await fixtures(context);
  await page.goto('/coaching-portal/setup');
  await expect(page.getByRole('heading', { name: 'Çalışma düzenimi kur', exact: true })).toBeVisible();
  await expect(page.getByLabel('Okul adı (isteğe bağlı)')).toHaveCount(0);
  await page.getByRole('combobox', { name: 'Eğitim durumu', exact: true }).selectOption('student');
  await page.getByLabel('Sınıf', { exact: true }).fill('12');
  await page.getByRole('button', { name: 'Profili kaydet', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Çalışma amacım', exact: true })).toBeVisible();
  await page.getByRole('combobox', { name: 'Çalışma amacım', exact: true }).selectOption('School');
  await page.getByRole('button', { name: 'Kaydet ve devam et', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Çalışma saatlerim', exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Çalışma saatlerim', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Saat aralığı ekle', exact: true }).click();
  await page.getByRole('button', { name: 'Çalışma saatlerini kaydet', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Çalışacağım dersler', exact: true })).toBeVisible();
  await page.getByRole('checkbox', { name: /Matematik/ }).check();
  await page.getByRole('button', { name: 'Dersleri kaydet ve devam et', exact: true }).click();
  await page.getByRole('combobox', { name: 'Ders', exact: true }).selectOption('lesson-1');
  await page.getByRole('combobox', { name: 'Sayılar / Doğal sayılar', exact: true }).selectOption('NeedsReview');
  await page.getByRole('checkbox', { name: 'Başlangıç bilgilerimi gözden geçirdim.' }).check();
  await page.getByRole('button', { name: 'Kaydet ve önizlemeye geç', exact: true }).click();
  await page.getByRole('combobox', { name: 'Seçtiğim ders', exact: true }).selectOption('lesson-1');
  await page.getByRole('button', { name: 'Konuları ara', exact: true }).click();
  await page.getByRole('button', { name: 'Konu ekle', exact: true }).click();
  await page.getByLabel('Başlangıç tarihi', { exact: true }).fill('2026-10-19');
  await page.getByRole('button', { name: 'Önizleme oluştur', exact: true }).click();
  await expect(page.getByText(/Yeni çalışmalar: 30 dk/)).toBeVisible();
  expect(previews).toHaveLength(1); expect(previews[0].expectedLearningContextVersion).toBe(2);
  await expect(page.getByText('Henüz kaydedilmedi', { exact: false })).toBeVisible();
  await page.screenshot({ path: `../../artifacts/coaching-learning-setup-tests/preview-${test.info().project.name}.png`, fullPage: true });
});

test('incomplete setup leaves the existing active plan accessible', async ({ page, context }) => {
  await fixtures(context);
  await page.goto('/coaching-portal/setup');
  await page.getByRole('link', { name: 'Mevcut planlarım', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Çalışma planlarım', exact: true })).toBeVisible();
  await page.getByRole('button', { name: /Mevcut koç planı/ }).click();
  await expect(page.getByText(/Tamamlandı · 25 dk/)).toBeVisible();
});
