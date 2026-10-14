import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

// Actual UI/engine, deterministic Identity/API fixtures; PostgreSQL persistence is tested separately.
const exerciseId = '30000000-0000-0000-0000-000000000001';
const typeId = '30000000-0000-0000-0000-000000000002';
const textId = '30000000-0000-0000-0000-000000000003';

async function prepare(page, role = 'Admin', pattern = 'radial', stimulusType = 'word', configuration) {
  const errors = [], writes = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  const exercise = { id: exerciseId, exerciseTypeId: typeId, title: 'Görsel Genişleme testi', difficultyLevel: 3,
    exerciseTypeName: 'VisualExpansion', configurationJson: JSON.stringify(configuration ?? { engineType: 'visual_expansion',
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


for (const [pattern, type] of [['radial','word'], ['vertical','symbol']]) test(`${pattern} ${type} preview has complete accessible answers and no false degree claim`, async ({ page }, testInfo) => {
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
    if (index + 1 < stimuli.length) await expect(inputs.nth(index + 1)).toBeFocused();
    await page.clock.runFor(20);
    const bounds = await inputs.nth(index).boundingBox();
    expect(bounds.x).toBeGreaterThanOrEqual(0);
    expect(bounds.x + bounds.width).toBeLessThanOrEqual(page.viewportSize().width);
  }
  await expect(submit).toBeEnabled();
  await submit.click();
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
  const responseTime = page.locator('.stat-card').filter({ hasText: 'Ortalama cevap süresi' }).locator('.stat-value');
  await expect(responseTime).toHaveText(/\d+[.,]\d{2} sn/);
  await expect(page.getByText('Hedef uzaklık', { exact: true })).toBeVisible();
  await expect(page.getByText('Hedefe ulaşıldı', { exact: true })).toHaveCount(0);
  await expect(page.getByText('Hedefe ulaşılmadı', { exact: true })).toBeVisible();
  await expect(page.getByText('Hedefteki doğruluk', { exact: true })).toBeVisible();
  await page.clock.runFor(5000);
  await page.screenshot({ path: testInfo.outputPath('result-overview.png'), animations: 'disabled' });
  const resultCards = page.locator('.result-card-new .stat-card');
  await expect(resultCards).toHaveCount(12);
  for (const card of await resultCards.all()) {
    await card.scrollIntoViewIfNeeded();
    const bounds = await card.boundingBox();
    expect(bounds.x).toBeGreaterThanOrEqual(0);
    expect(bounds.x + bounds.width).toBeLessThanOrEqual(page.viewportSize().width);
  }
  await page.getByText('Hedef uzaklık', { exact: true }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: testInfo.outputPath('target-summary.png'), animations: 'disabled' });
  expect(errors).toEqual([]); expect(writes).toEqual([]);
});

test('every correct preview answer expands by one and an incorrect answer holds position', async ({ page }) => {
  const { errors, writes } = await prepare(page, 'Admin', 'horizontal', 'letter', {
    engineType: 'visual_expansion', engineConfig: { rounds: 4, startDegrees: 8, targetDegrees: 16,
      displayDurationMs: 100, timing: { intervalMs: 100 }, content: { stimulusType: 'letter' }, mode: 'horizontal' }
  });
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install(); await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click();
  for (const [round, left] of [44, 43.25, 43.25, 42.5].entries()) {
    await page.clock.runFor(100);
    const stimuli = page.locator('.expansion-stimulus');
    await expect(stimuli.nth(0)).toHaveAttribute('style', new RegExp(`left: ${left}%`));
    const answers = (await stimuli.allTextContents()).map(value => value.trim());
    await page.clock.runFor(100);
    for (let index = 0; index < answers.length; index++) {
      await page.locator('.expansion-answer-area input').nth(index).fill(round === 1 ? '?' : answers[index]);
      await page.clock.runFor(20);
    }
    await page.getByRole('button', { name: /Onayla/ }).click();
  }
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
  expect(errors).toEqual([]); expect(writes).toEqual([]);
});

for (const age of ['child', 'teen', 'young-adult', 'adult']) {
  const catalog = JSON.parse(readFileSync(new URL(`../../../content-packs/${age}-exercises/v1/catalog.json`, import.meta.url)));
  for (const level of [1, 5]) test(`${age} level ${level} custom preview preserves nested catalog settings`, async ({ page }) => {
    const configuration = catalog.find(item => item.type === 'VisualExpansion' && item.difficultyLevel === level).configuration;
    const config = configuration.engineConfig;
    const { errors, writes } = await prepare(page, 'Teacher', 'horizontal', 'letter', configuration);
    await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByLabel('Tur sayısı', { exact: true })).toHaveValue(String(config.rounds));
    await expect(dialog.getByLabel('Gösterim süresi (ms)', { exact: true })).toHaveValue(String(config.displayDurationMs));
    await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
    await expect(page.locator('.start-button-large')).toBeVisible();
    await page.clock.install(); await page.clock.pauseAt(new Date());
    await page.locator('.start-button-large').click();
    for (let round = 0; round < 4; round++) {
      await page.clock.runFor(1500);
      await expect(page.getByText(`Tur ${round} / ${config.rounds}`, { exact: true })).toBeVisible();
      const stimuli = page.locator('.expansion-stimulus');
      const answers = (await stimuli.allTextContents()).map(value => value.trim());
      expect(answers).toHaveLength(2);
      await expect(stimuli.nth(0)).toHaveAttribute('style', new RegExp(`left: ${44 - round * .75}%`));
      await page.clock.runFor(config.displayDurationMs - 1);
      await expect(stimuli).toHaveCount(2);
      await page.clock.runFor(1);
      const inputs = page.locator('.expansion-answer-area input');
      for (let index = 0; index < answers.length; index++) {
        await inputs.nth(index).fill(answers[index]);
        await page.clock.runFor(20);
      }
      const submit = page.getByRole('button', { name: /Onayla/ });
      await expect(submit).toBeEnabled();
      await submit.click();
    }
    expect(errors).toEqual([]); expect(writes).toEqual([]);
  });
}

test('custom rounds and optional break preserve completed progress', async ({ page }) => {
  const { errors, writes } = await prepare(page, 'Admin', 'horizontal', 'letter', {
    engineType: 'visual_expansion', engineConfig: { rounds: 30, startDegrees: 8, targetDegrees: 8,
      displayDurationMs: 100, timing: { intervalMs: 100 }, content: { stimulusType: 'letter' }, mode: 'horizontal' }
  });
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Tur sayısı', { exact: true }).fill('16');
  await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install(); await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click();
  for (let round = 0; round < 15; round++) {
    await page.clock.runFor(100);
    const answers = (await page.locator('.expansion-stimulus').allTextContents()).map(value => value.trim());
    await page.clock.runFor(100);
    const inputs = page.locator('.expansion-answer-area input');
    for (let index = 0; index < answers.length; index++) {
      await inputs.nth(index).fill(answers[index]);
      await page.clock.runFor(20);
    }
    const submit = page.getByRole('button', { name: /Onayla/ });
    await expect(submit).toBeEnabled();
    await submit.click();
  }
  await expect(page.getByText('Tur 15 / 16', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Kısa mola ver', exact: true }).click();
  await page.clock.runFor(5000);
  await expect(page.getByText('Tur 15 / 16', { exact: true })).toBeVisible();
  await expect(page.locator('.expansion-stimulus')).toHaveCount(0);
  await page.getByRole('button', { name: 'play_arrow', exact: true }).click();
  await page.clock.runFor(100);
  const answers = (await page.locator('.expansion-stimulus').allTextContents()).map(value => value.trim());
  expect(answers).toHaveLength(2);
  await page.clock.runFor(100);
  for (let index = 0; index < answers.length; index++) {
    await page.locator('.expansion-answer-area input').nth(index).fill(answers[index]);
    await page.clock.runFor(20);
  }
  await page.getByRole('button', { name: /Onayla/ }).click();
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
  await expect(page.getByText('Hedefe ulaşıldı', { exact: true })).toBeVisible();
  const completed = page.locator('.stat-card').filter({ hasText: 'Tamamlanan / planlanan tur' });
  await expect(completed.locator('.stat-value')).toHaveText('16 / 16');
  expect(errors).toEqual([]); expect(writes).toEqual([]);
});
