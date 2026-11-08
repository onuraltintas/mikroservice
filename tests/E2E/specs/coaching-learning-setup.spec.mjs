import { test, expect } from '@playwright/test';

// These browser journeys exercise the production UI with isolated API fixtures.
// Real persistence, authorization and migration are covered by PostgreSQL / API tests.
const student = 'b8a09753-f3ea-44d8-a444-cf49577b4a01';
const token = () => {
  const encode = x => Buffer.from(JSON.stringify(x)).toString('base64url');
  return `${encode({ alg: 'HS256' })}.${encode({ sub: student, role: 'Student', platform_product: 'coaching', given_name: 'Test', family_name: 'Öğrenci', exp: Math.floor(Date.now() / 1000) + 900 })}.fixture`;
};
const onboarding = { status: 'Completed', nextStep: 7, savedSteps: [1, 2, 3, 4, 5, 6, 7], completedAt: '2026-11-01T00:00:00Z', academicYearStart: 2026,
  examSessions: [], allowedPurposes: ['Lgs'], acceptedInvitations: [],
  answers: { birthDate: '2012-05-01', homeProvinceId: '06', homeCity: 'Ankara', homeDistrictId: '0601', homeDistrict: 'Çankaya', educationStatus: 'Student',
    gradeLevel: 8, graduationYear: null, schoolName: null, purpose: null, primaryScoreType: null, foreignLanguage: null, targetExamSessionId: null,
    targetExamYear: null, diplomaGrade: null, diplomaGradeEstimated: false, target: null, targetLocked: false } };
async function fixtures(context) {
  // F2: the education is chosen on Profilim (or in the first-login wizard); this student is an 8th grader.
  let profile = { version: 1, educationStatus: 'Student', gradeLevel: 8, isActive: true, shareProgressWithTeachers: true };
  let learning = null; let hours = null; const saves = [];
  const next = () => profile.educationStatus.toLowerCase() === 'unknown' ? 'Profile' : !learning ? 'Purpose'
    : !hours?.windows.length ? 'Availability' : !learning.coursePreferences.length ? 'Courses' : !learning.startingStatusReviewed ? 'Starting' : 'Preview';
  const view = () => ({ context: learning, nextStep: next(), isReady: next() === 'Preview', missingFields: next() === 'Preview' ? [] : [next()], warnings: [] });
  const lesson = { id: 'lesson-1', name: 'Matematik', gradeNumber: 12, examCode: null, topicCount: 1, missingDurationCount: 0 };
  await context.route('**/api/**', async route => {
    const request = route.request(); const url = new URL(request.url()); const path = url.pathname;
    const respond = data => route.fulfill({ json: { success: true, data } });
    if (path === '/api/auth/refresh-token') return route.fulfill({ json: { accessToken: token(), tokenType: 'Bearer', expiresInMinutes: 15 } });
    // Paket C: the first-login wizard is finished, so the guard lets the student into the panel (shape as in coaching-goal-editor.spec.mjs).
    if (path.endsWith('/students/me/onboarding')) return respond(onboarding);
    // The shell's own calls on load.
    if (path.endsWith('/profiles/me/navigation')) return respond({ showAssignments: true, showSessions: true, showFees: true });
    if (path.endsWith('/messages/unread-count')) return route.fulfill({ json: { count: 0 } });
    if (path.endsWith('/profiles/me/student')) { profile = { ...profile, ...request.postDataJSON(), version: profile.version + 1 }; return respond(profile); }
    if (path.endsWith('/profiles/me')) return respond({ student: profile, teacher: null, memberships: [] });
    if (path.endsWith('/setup/catalog')) return respond({ curriculum: { id: 'curriculum', name: 'Test ders programı', versionLabel: 'v1' }, lessons: [lesson],
      topics: { items: url.searchParams.get('lessonId') ? [{ id: 'topic-1', lessonId: lesson.id, name: 'Doğal sayılar', lessonName: 'Matematik', unitName: 'Sayılar', estimatedMinutes: 30, gradeNumber: 12, examCode: null }] : [], totalCount: 1, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] });
    if (path.endsWith('/setup')) {
      if (request.method() === 'PUT') { const r = request.postDataJSON(); saves.push(r); learning = { ...r, id: 'context', version: (learning?.version ?? -1) + 1, coursePreferences: r.courses, topicStates: r.topics, curriculumVersionId: 'curriculum' }; }
      return respond(view());
    }
    if (path.endsWith('/availability')) { if (request.method() === 'PUT') hours = { ...request.postDataJSON(), version: (hours?.version ?? -1) + 1 }; return respond(hours); }
    // Paket H3/H4: no routine yet; the wizard's tempo options.
    if (path.endsWith('/routine')) return route.fulfill({ status: 404, json: { success: false, code: 'StudyPlanning.RoutineMissing' } });
    if (path.endsWith('/weekly-draft/tempo-options')) return respond({ planEnd: '2027-06-13', options: [
      { tempo: 'Relaxed', finishBy: '2027-06-13', neededWeeklyMinutes: 60, capacityWeeklyMinutes: 60, fits: true },
      { tempo: 'Intense', finishBy: '2027-03-01', neededWeeklyMinutes: 120, capacityWeeklyMinutes: 60, fits: false }] });
    if (path.endsWith('/weekly-draft')) return respond({ draft: null, lessons: [lesson] });
    // Paket H2: the plan preview asks the server for the weekly proposal.
    if (path.endsWith('/weekly-draft/proposal')) return respond({ mode: 'Replace', examFocus: null, slots: [{ id: 'proposal-1', lessonId: 'lesson-1', lessonName: 'Matematik', examCode: 'LGS', day: 'Monday', startMinute: 1080, durationMinutes: 60 }],
      lessons: [{ lessonId: 'lesson-1', lessonName: 'Matematik', examCode: 'LGS', existingMinutes: 0, proposedMinutes: 60, neededMinutes: 60, noTime: false }],
      freeMinutes: 60, neededMinutes: 60, shortageMinutes: 0, maxDayMinutes: 480, weeksUntilEnd: 30, endDate: '2027-06-01', middleSchool: true });
    if (path.endsWith('/plans/existing')) return respond({ id: 'existing', version: 1, title: 'Mevcut koç planı', status: 'Active', tasks: [{ id: 'old-task', title: 'Önceki çalışma', plannedDate: '2026-10-19', plannedMinutes: 30, isPinned: false, isCompleted: true, actualMinutes: 25 }] });
    if (path.endsWith('/plans')) return respond({ items: [{ id: 'existing', version: 1, title: 'Mevcut koç planı', status: 'Active' }], totalCount: 1, pageNumber: 1, pageSize: 20 });
    if (path.includes('assignments')) return respond({ items: [], totalCount: 0, pageNumber: 1, pageSize: 5 });
    return respond([]);
  });
  return { saves };
}

test('student resumes setup after reload and previews a plan from selected courses', async ({ page, context }) => {
  const { saves } = await fixtures(context);
  await page.goto('/coaching-portal/account/settings');
  await expect(page.getByRole('heading', { name: 'Çalışma tercihlerim', exact: true })).toBeVisible();
  await expect(page.getByLabel('Okul adı (isteğe bağlı)')).toHaveCount(0);
  // F2 (#27): the education is not edited here; a short line links to Profilim.
  await expect(page.getByRole('link', { name: 'Profilim', exact: true }).last()).toHaveAttribute('href', '/coaching-portal/account/settings/profile');
  await expect(page.getByRole('heading', { name: 'Sınavım', exact: true })).toBeVisible();
  await page.getByRole('combobox', { name: 'Hangi sınava hazırlanıyorsun?', exact: true }).selectOption('Lgs');
  await page.getByRole('combobox', { name: /^Hangi yıl sınava gireceksin\?/ }).selectOption({ index: 1 });
  await page.getByRole('button', { name: 'Kaydet ve devam et', exact: true }).click();
  await expect(page.locator('nav[aria-label="Kurulum adımları"] [aria-current=step]')).toHaveText('Müsaitliğim');
  await page.reload();
  await expect(page.locator('nav[aria-label="Kurulum adımları"] [aria-current=step]')).toHaveText('Müsaitliğim');
  await page.getByRole('button', { name: 'Saat aralığı ekle', exact: true }).click();
  await page.getByRole('button', { name: 'Çalışma saatlerini kaydet', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Çalışacağım dersler', exact: true })).toBeVisible();
  // F2 (#28): the exam's courses are suggested (ticked) until the student saves them.
  await expect(page.getByRole('checkbox', { name: /Matematik/ })).toBeChecked();
  await page.getByRole('button', { name: 'Dersleri kaydet ve devam et', exact: true }).click();
  await page.getByRole('combobox', { name: 'Ders', exact: true }).selectOption('lesson-1');
  await page.getByRole('region', { name: 'Sayılar ünitesi' }).getByRole('combobox', { name: 'Doğal sayılar – başlangıç durumu', exact: true }).selectOption({ label: 'Tekrar etmek istiyorum' });
  // F2 (#28): no "gözden geçirdim" box; saving the step is the confirmation.
  await expect(page.getByRole('checkbox', { name: 'Başlangıç bilgilerimi gözden geçirdim.' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Kaydet ve önizlemeye geç', exact: true }).click();
  // T3: the ready setup hands over to the plan wizard (Planım › Plan sihirbazı).
  await expect(page.getByText('Bilgilerin hazır.', { exact: false })).toBeVisible();
  await page.getByRole('link', { name: 'Plan sihirbazını aç', exact: true }).click();
  await expect(page).toHaveURL(/\/coaching-portal\/plan\/wizard$/);
  // Paket H4: the student has an active plan, so the wizard asks what to change; "Planı baştan kur" runs every step, starting with
  // the saved hours (no routine yet).
  await expect(page.getByRole('heading', { name: 'Planında neyi değiştirmek istiyorsun?', exact: true })).toBeVisible();
  await expect(page.getByText('Konulardan plan', { exact: false })).toHaveCount(0);
  await page.getByRole('button', { name: 'Planı baştan kur', exact: true }).click();
  await expect(page.getByTestId('wizard-progress')).toHaveText('Adım 1 / 4');
  await page.getByRole('button', { name: 'Bu saatlerle devam', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Derslerin', exact: true })).toBeVisible();
  await page.getByRole('radio', { name: 'Zor', exact: true }).check();
  await page.getByRole('button', { name: 'Devam', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tempo', exact: true })).toBeVisible();
  expect(saves.at(-1).courses).toEqual([{ lessonId: 'lesson-1', priority: 3, selfReportedDifficulty: 4 }]);
  expect(saves.at(-1).expectedVersion).toBe(2);
  await page.screenshot({ path: `../../artifacts/coaching-learning-setup-tests/preview-${test.info().project.name}.png`, fullPage: true });
});

test('incomplete setup leaves the existing active plan accessible', async ({ page, context }) => {
  await fixtures(context);
  await page.goto('/coaching-portal/account/settings');
  // T5 hides the setup's own shortcut links inside Profil ve ayarlar; the main menu (desktop) or quick bar (mobile) leads to Planım.
  await page.getByRole('link', { name: 'Planım', exact: true }).filter({ visible: true }).first().click();
  // T4: Planım opens the active plan by itself; its tasks are rows that open a detail sheet.
  await expect(page).toHaveURL(/\/coaching-portal\/plan(\?|$)/);
  await expect(page.locator('.plan-head .plan-status')).toContainText('Mevcut koç planı');
  await page.getByRole('tab', { name: 'Liste', exact: true }).click();
  await page.getByRole('button', { name: 'Önceki çalışma, ayrıntıları aç', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Önceki çalışma' }).getByText(/Tamamlandı · 25 dk/)).toBeVisible();
});
