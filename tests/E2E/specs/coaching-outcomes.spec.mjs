import { test, expect } from '@playwright/test';

const token = role => {
  const encode = x => Buffer.from(JSON.stringify(x)).toString('base64url');
  return `${encode({ alg: 'HS256' })}.${encode({ sub: 'b8a09753-f3ea-44d8-a444-cf49577b4a01', role, platform_product: 'coaching',
    permission: ['Permissions.Coaching.View', 'Permissions.Coaching.ContentManage'], given_name: 'Test', family_name: 'Kullanıcı', exp: Math.floor(Date.now() / 1000) + 900 })}.fixture`;
};
const outcome = { outcomeId: 'outcome', code: 'M.8.1.1.1', name: 'Çarpanları bulur', estimateId: 'estimate-2', estimateNumber: 2,
  learningMinutes: 20, practiceMinutes: 30, checkMinutes: 10, totalMinutes: 60, editionCode: 'meb-2018', officialLessonPeriods: 10 };

test('student selects reviewed outcomes, overrides time and saves and completes work', async ({ context, page }) => {
  let plan = null; let previewRequest; const completed = [];
  await context.route('**/api/**', async route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    const respond = data => route.fulfill({ json: { success: true, data } });
    if (path.endsWith('/auth/refresh-token')) return route.fulfill({ json: { accessToken: token('Student'), tokenType: 'Bearer', expiresInMinutes: 15 } });
    if (path.endsWith('/profiles/me')) return respond({ student: { educationStatus: 'Student', gradeLevel: 8, isActive: true }, memberships: [] });
    if (path.endsWith('/availability')) return respond({ version: 1, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1140 }] });
    if (path.endsWith('/topics')) return respond({ items: [{ id: 'topic', name: 'Çarpanlar ve Katlar', lessonName: 'Matematik', unitName: 'LGS', examCode: 'LGS', gradeNumber: null, estimatedMinutes: null }], totalCount: 1, pageNumber: 1, pageSize: 20 });
    if (path.endsWith('/topics/topic/outcomes')) return respond([outcome]);
    if (path.endsWith('/automatic-preview')) {
      previewRequest = request.postDataJSON();
      return respond({ availabilityVersion: 1, timeZoneId: 'Europe/Istanbul', activeRevisionId: null, activeRevisionVersion: null, protectedTasks: [],
        schedule: { tasks: [{ topicId: 'topic', plannedDate: '2026-10-19', plannedMinutes: 35 }], unscheduledTopics: [], scheduledMinutes: 35, unusedMinutes: 85, unscheduledMinutes: 0 } });
    }
    if (path.endsWith('/automatic-drafts')) {
      const r = request.postDataJSON(); expect(r.preview.topics).toEqual(previewRequest.topics);
      plan = { id: 'pilot', version: 1, title: r.title, status: 'Draft', tasks: [{ id: 'work', plannedDate: '2026-10-19', title: 'Çarpanlar ve Katlar', plannedMinutes: 35, isCompleted: false, isPinned: false,
        outcomes: [{ ...outcome, allocatedMinutes: 35, origin: 'StudentOverride' }] }] }; return respond(plan);
    }
    if (path.endsWith('/plans/pilot/publish')) { plan = { ...plan, status: 'Active', version: 2 }; return respond(plan); }
    if (path.endsWith('/completion')) { completed.push(request.postDataJSON()); plan = { ...plan, version: 3, tasks: [{ ...plan.tasks[0], isCompleted: true, actualMinutes: 32, actualQuestions: 12, completedAt: '2026-10-19T15:00:00Z' }] }; return respond(plan); }
    if (path.endsWith('/plans/pilot')) return respond(plan);
    if (path.endsWith('/plans')) return respond({ items: plan ? [plan] : [], totalCount: plan ? 1 : 0, pageNumber: 1, pageSize: 20 });
    return respond([]);
  });
  await page.goto('/coaching-portal/automatic-plan');
  await page.getByRole('button', { name: 'Konuları ara', exact: true }).click();
  await page.getByRole('button', { name: 'Konu ekle', exact: true }).click();
  await page.getByRole('button', { name: 'Kazanım önerilerini yükle', exact: true }).click();
  await expect(page.getByText('20 dk öğrenme', { exact: false })).toBeVisible();
  await page.getByLabel('Bu kazanıma ayıracağım dakika').fill('35');
  await page.getByLabel('Başlangıç tarihi', { exact: true }).fill('2026-10-19');
  await page.getByRole('button', { name: 'Önizleme oluştur', exact: true }).click();
  await expect(page.getByText(/Yeni çalışmalar: 35 dk/)).toBeVisible();
  expect(previewRequest.topics[0].outcomes).toEqual([{ outcomeId: 'outcome', estimateId: 'estimate-2', requiredMinutes: 35 }]);
  await page.getByLabel('Plan başlığı', { exact: true }).fill('LGS kazanım pilotu');
  await page.getByRole('checkbox', { name: /Önizlemeyi kontrol ettim/ }).check();
  await page.getByRole('button', { name: 'Taslağı kaydet', exact: true }).click();
  await expect(page.getByText(/taslak olarak kaydedildi/)).toBeVisible();
  await page.getByRole('link', { name: 'Çalışma planlarımı aç', exact: true }).click();
  await page.getByRole('button', { name: /LGS kazanım pilotu/ }).click();
  await page.getByRole('checkbox', { name: /Planımı kontrol ettim/ }).check();
  await page.getByRole('button', { name: 'Planı yayımla', exact: true }).click();
  await expect(page.getByText(/M.8.1.1.1.*süre sürümü 2/)).toBeVisible();
  await page.getByLabel('Gerçek süre (dk)', { exact: true }).fill('32');
  await page.getByLabel('Gerçek soru (isteğe bağlı)', { exact: true }).fill('12');
  await page.getByRole('button', { name: 'Tamamla', exact: true }).click();
  await expect(page.getByText(/Tamamlandı · 32 dk/)).toBeVisible(); expect(completed).toHaveLength(1);
  await page.screenshot({ path: `../../artifacts/coaching-outcomes/student-${test.info().project.name}.png`, fullPage: true });
});

test('administrator previews a draft package and must supply an import reason', async ({ context, page }) => {
  const imports = [];
  await context.route('**/api/**', async route => {
    const r = route.request(); const path = new URL(r.url()).pathname; const respond = data => route.fulfill({ json: { success: true, data } });
    if (path.endsWith('/auth/refresh-token')) return route.fulfill({ json: { accessToken: token('SystemAdmin'), tokenType: 'Bearer', expiresInMinutes: 15 } });
    if (path.endsWith('/outcomes/import-preview')) return respond({ fingerprint: 'reviewed', outcomeCount: 52, matchedTopics: 13, unmatchedTopics: [], warnings: ['İnceleme gereklidir.'] });
    if (path.endsWith('/outcomes/import')) { imports.push(r.postDataJSON()); return respond({}); }
    if (path.endsWith('/outcomes')) return respond([]);
    if (path.includes('/catalog/lessons')) return respond({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
    return respond([]);
  });
  await page.goto('/dashboard/coaching/catalog');
  await page.getByText('LGS kazanımları ve süre incelemesi', { exact: true }).click();
  await page.getByText('Kazanım paketi aktar', { exact: true }).click();
  await page.getByLabel('Paket JSON', { exact: true }).fill('{"code":"pilot"}');
  await page.getByRole('button', { name: 'Aktarımı önizle', exact: true }).click();
  await expect(page.getByText(/52 kazanım · 13 eşleşen konu/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Taslakları aktar', exact: true })).toBeDisabled();
  await page.getByLabel('İşlem gerekçesi', { exact: true }).fill('LGS pilot kontrolü');
  await page.getByRole('button', { name: 'Taslakları aktar', exact: true }).click();
  await expect.poll(() => imports.length).toBe(1); expect(imports[0].fingerprint).toBe('reviewed');
});
