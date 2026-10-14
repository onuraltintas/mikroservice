import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

for (const scenario of [{ label: '500 ms custom preview', length: 4, duration: 500, rounds: 25 },
  { label: '75 ms custom preview', length: 3, duration: 75, rounds: 15 },
  { label: 'default preview', length: 4, duration: 400, rounds: 30, defaults: true }]) {
test(`adaptive child ${scenario.label} keeps digit count fixed and completes without saving`, async ({ page }) => {
  const patch = JSON.parse(readFileSync(new URL('../../../content-packs/tachistoscope-settings/v2/settings.json', import.meta.url), 'utf8'))
    .find(item => item.age === 'Child' && item.level === 5);
  const id = patch.id; const typeId = '40000000-0000-0000-0000-000000000002';
  const errors = []; const writes = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  const exercise = { id, exerciseTypeId: typeId, title: 'Takistoskop', difficultyLevel: 5,
    exerciseTypeName: 'Tachistoscope', configurationJson: JSON.stringify(patch.configuration) };
  if (scenario.defaults) {
    const config = structuredClone(patch.configuration);
    Object.assign(config.engineConfig.content, { length: scenario.length, count: scenario.rounds });
    config.engineConfig.timing.durationMs = scenario.duration;
    exercise.configurationJson = JSON.stringify(config);
  }
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    if (route.request().method() !== 'GET' && /session|result|progress/.test(path)) writes.push(path);
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role: 'Admin' };
      response = { id: 'test-user', roles: ['Admin'], token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture` };
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Tachistoscope', displayName: 'Takistoskop', engineType: 'text_stream', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${id}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  if (scenario.defaults) {
    await page.goto('/student/exercises/universal-player/' + id, { waitUntil: 'domcontentloaded' });
  } else {
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  const dialog = page.getByRole('dialog');
  const length = dialog.getByLabel('Başlangıç basamak sayısı');
  await expect(length).toHaveValue('5');
  await expect(length).toHaveAttribute('min', '3'); await expect(length).toHaveAttribute('max', '5');
  await length.fill('6'); await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(dialog.getByRole('alert')).toContainText('geçerli bir değer');
  const displayDuration = dialog.getByLabel('Başlangıç gösterim süresi (ms)');
  await expect(displayDuration).toHaveAttribute('min', '75');
  await displayDuration.fill('74'); await length.fill(String(scenario.length));
  await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(dialog.getByRole('alert')).toContainText('75');
  await displayDuration.fill(String(scenario.duration));
  await dialog.getByLabel('Uyaran sayısı').fill(String(scenario.rounds));
  await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
  }
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install(); await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click(); await page.clock.runFor(300);
  let duration = scenario.duration;
  for (let round = 0; round < scenario.rounds; round++) {
    const stimulus = await page.locator('.stimulus-display').innerText();
    const digits = scenario.length;
    expect(stimulus).toMatch(new RegExp(`^\\d{${digits}}$`));
    await page.clock.runFor(duration - 1);
    await expect(page.locator('.stimulus-display')).toBeVisible();
    await page.clock.runFor(1);
    await expect(page.locator('.stimulus-display')).toHaveCount(0);
    await page.locator('.answer-input').fill(stimulus); await page.locator('.submit-btn').click();
    if ((round + 1) % 5 === 0) duration = Math.max(75, Math.round(duration * .9));
    if (round < scenario.rounds - 1) await page.clock.runFor(1050); // 750ms feedback + 300ms fixation.
  }
  await page.clock.runFor(600);
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: false })).toBeVisible();
  expect(writes).toEqual([]); expect(errors).toEqual([]);
});
}

test('feedback never displaces fixation or stimulus and counters update', async ({ page }) => {
  const id = '40000000-0000-0000-0000-000000000001';
  const typeId = '40000000-0000-0000-0000-000000000002';
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  const exercise = { id, exerciseTypeId: typeId, title: 'Takistoskop', difficultyLevel: 1,
    exerciseTypeName: 'Tachistoscope', configurationJson: JSON.stringify({ engineType: 'text_stream',
      engineConfig: { mode: 'flash', Words: ['bir', 'iki', 'ses'], content: { count: 3 },
        timing: { durationMs: 100, intervalMs: 0 }, adaptive: { enabled: false } } }) };
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role: 'Admin' };
      response = { id: 'test-user', roles: ['Admin'], token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture` };
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: 'Tachistoscope', displayName: 'Takistoskop', engineType: 'text_stream', isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${id}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  await page.clock.install();
  await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click();
  await expect(page.locator('.fixation-cross')).toBeVisible();
  const center = await page.locator('.fixation-cross').boundingBox();
  await page.clock.runFor(330);
  const word = await page.locator('.stimulus-display').innerText();
  const stimulus = await page.locator('.stimulus-display').boundingBox();
  expect(Math.abs(stimulus.x + stimulus.width / 2 - center.x - center.width / 2)).toBeLessThan(2);
  expect(Math.abs(stimulus.y + stimulus.height / 2 - center.y - center.height / 2)).toBeLessThan(2);
  await page.clock.runFor(600);
  await page.locator('.answer-input').fill(word);
  await page.locator('.submit-btn').click();
  await expect(page.locator('.feedback-display')).toContainText('Doğru!');
  await expect(page.locator('.fixation-cross')).toHaveCount(0);
  await expect(page.locator('.game-stats .stat').filter({ hasText: 'Doğru' })).toContainText('1');
  await expect(page.locator('.game-stats .stat').filter({ hasText: 'Yanlış' })).toContainText('0');
  await page.clock.runFor(600);
  await expect(page.locator('.feedback-display')).toHaveCount(0);
  await expect(page.locator('.fixation-cross')).toBeVisible();
  expect(errors).toEqual([]);
});
