import { test, expect } from '@playwright/test';

// Deterministic browser checks of the real UI/engine. API and Identity are fixtures;
// server persistence and authorization are tested separately against PostgreSQL.
const exerciseId = '10000000-0000-0000-0000-000000000001';
const typeId = '10000000-0000-0000-0000-000000000002';
const textId = '10000000-0000-0000-0000-000000000003';
const questionId = '10000000-0000-0000-0000-000000000004';
const exercise = {
  id: exerciseId, exerciseTypeId: typeId, title: 'Anlama tarayıcı testi', difficultyLevel: 1,
  configurationJson: JSON.stringify({ engineType: 'reading_comprehension', readingTextId: textId,
    engineConfig: { timing: { minReadingTimeMs: 1000, maxReadingTimeMs: 0 }, display: { lineHeight: 1.8 } } })
};

async function prepare(page, role = 'Admin') {
  const writes = [];
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
  await page.route('**/api/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    let response = {};
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: [role], firstName: 'Test' };
    } else if (request.method() !== 'GET') {
      writes.push(path);
      return route.fulfill({ status: 500, json: { message: 'Unexpected preview write' } });
    } else if (path.endsWith('/exercise-types')) {
      response = { items: [{ id: typeId, name: 'Comprehension', displayName: 'Anlama', engineType: 'reading_comprehension', isActive: true }], totalCount: 1 };
    } else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1, pageNumber: 1, pageSize: 100 };
    else if (path.endsWith(`/reading-texts/${textId}`)) response = {
      id: textId, title: 'Okuma metni', content: Array(50).fill('Okuma dikkati geliştirir.').join(' '), wordCount: 150,
      questions: [{ id: questionId, questionText: 'Metinde ne anlatılıyor?', correctAnswer: 'A', optionA: 'Okuma', optionB: 'Spor', optionC: 'Müzik', optionD: 'Uyku' }]
    };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    else response = { items: [], totalCount: 0 };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises');
  await expect(page.getByRole('heading', { name: 'Anlama', exact: true })).toBeVisible();
  return { writes, errors };
}

async function openCustom(page, maximum = '0') {
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('heading', { name: 'Özel ayarlarla dene' })).toBeVisible();
  await expect(dialog.locator('select')).toHaveCount(1); // Font only: no level picker.
  await dialog.getByLabel('Metin boyutu').selectOption('large');
  await dialog.getByLabel('Minimum okuma süresi (saniye)').fill(maximum === '0' ? '2' : '0');
  await dialog.getByLabel('Maksimum okuma süresi (saniye; 0: sınırsız)').fill(maximum);
  await dialog.getByLabel('Satır aralığı (%)').fill('220');
  await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.player-container > [role="status"]')).toContainText('kaydedilmez');
  await page.locator('.start-button-large').click();
}

async function answer(page) {
  await expect(page.locator('.question-text')).toHaveText('Metinde ne anlatılıyor?');
  await page.locator('.answer-option').first().click();
  await page.getByRole('button', { name: 'Sonuçları Gör' }).click();
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
}

test('custom settings, minimum-time gate, questions and result without writes', async ({ page }) => {
  const { writes, errors } = await prepare(page);
  await openCustom(page);
  const finish = page.locator('.complete-reading-btn');
  await expect(finish).toBeDisabled();
  await expect(page.locator('.reading-text-content')).toHaveClass(/font-large/);
  await expect(page.locator('.reading-text-content')).toHaveCSS('line-height', /.+/);
  const ratio = await page.locator('.reading-text-content').evaluate(element => {
    const style = getComputedStyle(element);
    return parseFloat(style.lineHeight) / parseFloat(style.fontSize);
  });
  expect(ratio).toBeCloseTo(2.2, 1);
  await expect(finish).toBeEnabled();
  await finish.click();
  await answer(page);
  expect(writes).toEqual([]);
  expect(errors).toEqual([]);
});

test('timeout retains incomplete-reading warning after questions', async ({ page }) => {
  const { writes, errors } = await prepare(page);
  await openCustom(page, '1');
  await answer(page);
  await expect(page.getByText(/Okuma tamamlanmadı/).first()).toBeVisible();
  expect(writes).toEqual([]);
  expect(errors).toEqual([]);
});

test('invalid duration cannot launch and reset restores catalogue settings', async ({ page }) => {
  await prepare(page);
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Minimum okuma süresi (saniye)').fill('10');
  await dialog.getByLabel('Maksimum okuma süresi (saniye; 0: sınırsız)').fill('5');
  await dialog.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(dialog.getByRole('alert')).toHaveText('Maksimum okuma süresi minimum süreden kısa olamaz.');
  await dialog.getByRole('button', { name: 'Varsayılana dön' }).click();
  await expect(dialog.getByLabel('Minimum okuma süresi (saniye)')).toHaveValue('1');
  await expect(dialog.getByLabel('Satır aralığı (%)')).toHaveValue('180');
});

test('teacher can preview and paused time does not unlock completion', async ({ page }) => {
  const { writes, errors } = await prepare(page, 'Teacher');
  await page.clock.install();
  await openCustom(page);
  const finish = page.locator('.complete-reading-btn');
  const pause = page.locator('.reading-comprehension-container .game-header button').first();
  await pause.click();
  await page.clock.runFor(3000);
  await expect(finish).toBeDisabled();
  await expect(page.locator('.reading-text-area')).toHaveClass(/paused/);
  await pause.click();
  await page.clock.runFor(2100);
  await expect(finish).toBeEnabled();
  await finish.click();
  await answer(page);
  expect(writes).toEqual([]);
  expect(errors).toEqual([]);
});

test('student does not see staff custom-preview actions', async ({ page }) => {
  await prepare(page, 'Student');
  await expect(page.getByRole('button', { name: 'Özel ayarlarla dene' })).toHaveCount(0);
});
