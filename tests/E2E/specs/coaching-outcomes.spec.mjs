import { test, expect } from '@playwright/test';
import { studentShell } from '../support/coaching-student-shell.mjs';

const token = role => {
  const encode = x => Buffer.from(JSON.stringify(x)).toString('base64url');
  return `${encode({ alg: 'HS256' })}.${encode({ sub: 'b8a09753-f3ea-44d8-a444-cf49577b4a01', role, platform_product: 'coaching',
    permission: ['Permissions.Coaching.View', 'Permissions.Coaching.ContentManage'], given_name: 'Test', family_name: 'Kullanıcı', exp: Math.floor(Date.now() / 1000) + 900 })}.fixture`;
};
const outcome = { outcomeId: 'outcome', code: 'M.8.1.1.1', name: 'Çarpanları bulur', estimateId: 'estimate-2', estimateNumber: 2,
  learningMinutes: 20, practiceMinutes: 30, checkMinutes: 10, totalMinutes: 60, editionCode: 'meb-2018', officialLessonPeriods: 10 };

// Paket H4 (K3): "Konulardan plan" left the wizard. The old address still opens the wizard, and a topic plan saved earlier (its server
// endpoints stay) is still published and completed on Planım with its outcomes.
test('the old topic-plan address opens the wizard; a saved topic draft is published and completed with its outcomes', async ({ context, page }) => {
  const completed = [];
  let plan = { id: 'pilot', version: 1, title: 'LGS kazanım pilotu', status: 'Draft', tasks: [{ id: 'work', plannedDate: '2026-10-19', title: 'Çarpanlar ve Katlar', plannedMinutes: 35, isCompleted: false, isPinned: false,
    outcomes: [{ ...outcome, allocatedMinutes: 35, origin: 'StudentOverride' }] }] };
  await context.route('**/api/**', async route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    const respond = data => route.fulfill({ json: { success: true, data } });
    if (path.endsWith('/auth/refresh-token')) return route.fulfill({ json: { accessToken: token('Student'), tokenType: 'Bearer', expiresInMinutes: 15 } });
    const shell = studentShell(route, path); if (shell) return shell;
    if (path.endsWith('/profiles/me')) return respond({ student: { educationStatus: 'Student', gradeLevel: 8, isActive: true }, memberships: [] });
    if (path.endsWith('/weekly-draft')) return respond({ draft: null, lessons: [] });
    if (path.endsWith('/availability')) return respond({ version: 1, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1140 }] });
    if (path.endsWith('/routine')) return route.fulfill({ status: 404, json: { success: false, code: 'StudyPlanning.RoutineMissing' } });
    if (path.endsWith('/plans/pilot/publish')) { plan = { ...plan, status: 'Active', version: 2 }; return respond(plan); }
    if (path.endsWith('/completion')) { completed.push(request.postDataJSON()); plan = { ...plan, version: 3, tasks: [{ ...plan.tasks[0], isCompleted: true, actualMinutes: 32, actualQuestions: 12, completedAt: '2026-10-19T15:00:00Z' }] }; return respond(plan); }
    if (path.endsWith('/plans/pilot')) return respond(plan);
    if (path.endsWith('/plans')) return respond({ items: plan ? [plan] : [], totalCount: plan ? 1 : 0, pageNumber: 1, pageSize: 20 });
    return respond([]);
  });
  await page.goto('/coaching-portal/automatic-plan');
  await expect(page).toHaveURL(/\/coaching-portal\/plan\/wizard$/);
  await expect(page.getByRole('heading', { name: 'Planını adım adım hazırla', exact: true })).toBeVisible();
  await expect(page.getByText('Konulardan plan', { exact: false })).toHaveCount(0);
  await page.goto('/coaching-portal/plan');
  // T4: with no active plan Planım offers the saved plans under "Kayıtlı planlarım"; a draft opens in its editor and is published there.
  await page.getByText('Kayıtlı planlarım', { exact: true }).click();
  await page.getByRole('button', { name: /LGS kazanım pilotu/ }).click();
  await expect(page.getByRole('heading', { name: 'Taslağını düzenle', exact: true })).toBeVisible();
  await page.getByRole('checkbox', { name: /Planımı kontrol ettim/ }).check();
  await page.getByRole('button', { name: 'Planı yayımla', exact: true }).click();
  await page.getByRole('button', { name: 'Çarpanlar ve Katlar, ayrıntıları aç', exact: true }).click();
  const sheet = page.getByRole('dialog', { name: 'Çarpanlar ve Katlar' });
  await expect(sheet.getByText(/M.8.1.1.1.*süre sürümü 2/)).toBeVisible();
  await sheet.getByLabel('Gerçek süre (dk)', { exact: true }).fill('32');
  await sheet.getByLabel('Gerçek soru (isteğe bağlı)', { exact: true }).fill('12');
  await sheet.getByRole('button', { name: 'Tamamla', exact: true }).click();
  await expect(sheet.getByText(/Tamamlandı · 32 dk/)).toBeVisible(); expect(completed).toHaveLength(1);
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
  // KM7b (ca53dc34): outcome packages moved from the Katalog page to Müfredat Yönetimi › Aktarım ve sürümler.
  await page.goto('/dashboard/coaching/curriculum');
  await page.getByRole('tab', { name: 'Aktarım ve sürümler', exact: true }).click();
  await page.getByText('Kazanım paketleri ve süre incelemesi', { exact: true }).click();
  const outcomes = page.locator('app-coaching-outcomes');
  await outcomes.getByText('Kazanım paketi aktar', { exact: true }).click();
  await outcomes.getByLabel('Paket JSON', { exact: true }).fill('{"code":"pilot"}');
  await outcomes.getByRole('button', { name: 'Aktarımı önizle', exact: true }).click();
  await expect(outcomes.getByText(/52 kazanım · 13 eşleşen konu/)).toBeVisible();
  await expect(outcomes.getByRole('button', { name: 'Taslakları aktar', exact: true })).toBeDisabled();
  await outcomes.getByLabel('İşlem gerekçesi', { exact: true }).fill('LGS pilot kontrolü');
  await outcomes.getByRole('button', { name: 'Taslakları aktar', exact: true }).click();
  await expect.poll(() => imports.length).toBe(1); expect(imports[0].fingerprint).toBe('reviewed');
});
