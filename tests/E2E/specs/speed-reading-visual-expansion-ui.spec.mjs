import { test, expect } from '@playwright/test';

// Actual UI/engine, deterministic Identity/API fixtures; PostgreSQL persistence is tested separately.
const exerciseId = '30000000-0000-0000-0000-000000000001';
const typeId = '30000000-0000-0000-0000-000000000002';
const textId = '30000000-0000-0000-0000-000000000003';

async function prepare(page, role = 'Admin', pattern = 'radial', stimulusType = 'word') {
  const errors = [], writes = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  const exercise = { id: exerciseId, exerciseTypeId: typeId, title: 'Görsel Genişleme testi', difficultyLevel: 3,
    exerciseTypeName: 'VisualExpansion', configurationJson: JSON.stringify({ engineType: 'visual_expansion',
      rounds: 1, startDegrees: 30, targetDegrees: 40,
      engineConfig: { expansion: { pattern, stimulusType },
        visuals: { centerPoint: 'circle', stimulusSize: '2rem' }, timing: { durationMs: 1000, intervalMs: 100 } } }) };
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role] };
    } else if (request.method() !== 'GET') {
      writes.push(path);
      const dailyProgress = role === 'Student' && path === '/api/speed-reading/daily-progress/complete-exercise';
      return route.fulfill({ status: dailyProgress ? 200 : 500,
        json: dailyProgress ? {} : { message: 'Unexpected fixture write' } });
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'VisualExpansion', displayName: 'Görsel Genişleme', engineType: 'visual_expansion', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.endsWith(`/reading-texts/${textId}`)) response = { id: textId, title: 'Metin', content: 'bir iki üç dört beş altı', wordCount: 6, questions: [] };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await expect(page.getByRole('heading', { name: 'Görsel Genişleme', exact: true })).toBeVisible();
  return { errors, writes };
}


for (const [pattern, type] of [['radial','word'], ['vertical','symbol']]) test(`${pattern} ${type} preview has complete accessible answers and no false degree claim`, async ({ page }) => {
  const { errors, writes } = await prepare(page, 'Admin', pattern, type);
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install(); await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click(); await page.clock.runFor(100);
  const stimuli = await page.locator('.expansion-stimulus').allTextContents();
  expect(stimuli).toHaveLength(pattern === 'radial' ? 4 : 2);
  await expect(page.locator('.center-circle')).toBeVisible();
  await expect(page.getByText('Göreli uzaklık', { exact: true })).toBeVisible();
  await expect(page.getByText('Görüş Açısı', { exact: true })).toHaveCount(0);
  await page.clock.runFor(1000);
  const inputs = page.locator('.expansion-answer-area input');
  await expect(inputs).toHaveCount(stimuli.length);
  const submit = page.getByRole('button', { name: /Onayla/ });
  await expect(submit).toBeDisabled();
  for (let index = 0; index < stimuli.length; index++) {
    await inputs.nth(index).fill(stimuli[index].trim());
    await page.clock.runFor(20);
    const bounds = await inputs.nth(index).boundingBox();
    expect(bounds.x).toBeGreaterThanOrEqual(0);
    expect(bounds.x + bounds.width).toBeLessThanOrEqual(page.viewportSize().width);
  }
  await expect(submit).toBeEnabled();
  await submit.click();
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
  expect(errors).toEqual([]); expect(writes).toEqual([]);
});
