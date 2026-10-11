import { test, expect } from '@playwright/test';

// Identity is stubbed only in this local test; the real application guards still run.
async function prepare(page, roles = ['Teacher']) {
  await page.clock.install();
  const errors = [], writes = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'attention-test' })));
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'attention-test', exp: Math.floor(Date.now() / 1000) + 3600, role: roles };
      response = { id: 'attention-test', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles };
    } else if (request.method() !== 'GET') {
      writes.push(path); return route.fulfill({ status: 500, json: { message: 'Unexpected write' } });
    } else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/attention-preview', { waitUntil: 'domcontentloaded' });
  return { errors, writes };
}

async function practice(page) {
  await page.getByRole('button', { name: 'Alıştırmayı başlat' }).click();
  await page.clock.runFor(100);
  for (let round = 0; round < 3; round++) {
    await page.getByRole('button', { name: /^Yıldız, konum/ }).click();
    await page.clock.runFor(800);
  }
  await expect(page.getByRole('heading', { name: 'Hazırsınız!' })).toBeVisible();
}

async function noOverflow(page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
}

async function pauseClock(page) {
  // Base the pause on the browser clock, not a potentially skewed host clock.
  await page.clock.pauseAt(new Date(await page.evaluate(() => Date.now() + 10000)));
}

test('simple practice, correct wrong missed and completion without writes', async ({ page }, testInfo) => {
  const { errors, writes } = await prepare(page, ['Admin']);
  await expect(page.getByRole('heading', { name: 'Hedefi Yakala.' })).toBeVisible();
  await noOverflow(page);
  await page.screenshot({ path: `../../artifacts/attention-ui/${testInfo.project.name}-intro.png`, fullPage: true });
  await pauseClock(page);
  await page.getByRole('button', { name: 'Alıştırmayı başlat' }).click();
  await page.clock.runFor(20000);
  await expect(page.getByRole('heading', { name: 'Tur 1 / 3' })).toBeVisible();
  await page.getByRole('button', { name: /Daire|Üçgen|Kare|Baklava|Artı/ }).first().click();
  await page.clock.runFor(100);
  await expect(page.getByRole('status')).toContainText('tekrar deneyin');
  for (let round = 0; round < 3; round++) {
    await page.getByRole('button', { name: /^Yıldız, konum/ }).click();
    await page.clock.runFor(800);
  }
  await page.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await page.clock.runFor(100);
  await expect(page.locator('.shape-button')).toHaveCount(6);
  const target = page.getByRole('img', { name: 'Aranacak sembol: yıldız' });
  await expect(target).toBeVisible();
  const targetBounds = await target.boundingBox();
  const initialTime = await page.locator('.time').innerText();
  await page.clock.runFor(500);
  expect(await page.locator('.time').innerText()).not.toBe(initialTime);
  await page.getByRole('button', { name: /^Yıldız, konum/ }).click();
  await page.clock.runFor(100);
  await expect(page.locator('.counters')).toContainText('Doğru1');
  expect(await target.boundingBox()).toEqual(targetBounds);
  await page.clock.runFor(800);
  await page.getByRole('button', { name: /Daire|Üçgen|Kare|Baklava|Artı/ }).first().click();
  await page.clock.runFor(100);
  await expect(page.locator('.counters')).toContainText('Yanlış1');
  await page.clock.runFor(800); await page.clock.runFor(6000);
  await expect(page.locator('.counters')).toContainText('Kaçırılan1');
  await page.clock.runFor(800);
  await page.getByRole('button', { name: 'Duraklat' }).click();
  await page.clock.runFor(100);
  await noOverflow(page);
  await page.screenshot({ path: `../../artifacts/attention-ui/${testInfo.project.name}-paused.png`, fullPage: true });
  await page.clock.runFor(20000);
  await expect(page.locator('.shape-button').first()).toBeDisabled();
  await expect(target).toBeVisible();
  expect(await target.boundingBox()).toEqual(targetBounds);
  await page.getByRole('button', { name: 'Devam et', exact: true }).click();
  await page.clock.runFor(100);
  await page.getByRole('button', { name: /^Yıldız, konum/ }).focus();
  await page.keyboard.press('Enter'); await page.clock.runFor(800);
  await noOverflow(page);
  await page.screenshot({ path: `../../artifacts/attention-ui/${testInfo.project.name}-playing.png`, fullPage: true });
  for (let round = 5; round <= 24; round++) {
    await page.getByRole('button', { name: /^Yıldız, konum/ }).click(); await page.clock.runFor(800);
  }
  await expect(page.getByRole('heading', { name: 'Hedeflerin 22 tanesini yakaladınız.' })).toBeVisible();
  await expect(page.locator('.results')).toContainText('%92');
  await expect(page.locator('.results')).toContainText('sn');
  await expect(page.locator('.summary-panel')).toContainText('KAYDEDİLMEDİ');
  await page.screenshot({ path: `../../artifacts/attention-ui/${testInfo.project.name}-result.png`, fullPage: true });
  await page.getByRole('button', { name: 'Yeniden dene' }).click();
  await page.clock.runFor(100);
  await expect(page.getByRole('button', { name: 'Alıştırmayı başlat' })).toBeVisible();
  expect(writes).toEqual([]); expect(errors).toEqual([]);
});

for (const [group, level, count, rounds, seconds] of [['Çocuk', 4, 12, 36, 4.5], ['Genç yetişkin', 5, 20, 48, 3]]) {
  test(`${group} level ${level} applies settings and visible target with no layout overflow`, async ({ page }, testInfo) => {
    const { errors, writes } = await prepare(page);
    await page.getByRole('button', { name: group, exact: true }).click();
    await page.getByRole('button', { name: `Seviye ${level}`, exact: true }).click();
    await expect(page.locator('.settings-summary')).toContainText(`${rounds} tur`);
    await pauseClock(page);
    await practice(page); await page.getByRole('button', { name: 'Denemeyi başlat' }).click();
    await page.clock.runFor(100);
    await expect(page.locator('.shape-button')).toHaveCount(count);
    await expect(page.getByRole('heading', { name: `Tur 1 / ${rounds}` })).toBeVisible();
    const remaining = Number((await page.locator('.time').innerText()).replace(',', '.').replace(' sn', ''));
    expect(remaining).toBeLessThanOrEqual(seconds); expect(remaining).toBeGreaterThan(seconds - .5);
    const target = page.getByRole('img', { name: 'Aranacak sembol: yıldız' });
    await expect(target).toBeVisible();
    const cue = await target.boundingBox(), board = await page.locator('.board-wrap').boundingBox();
    expect(cue.y + cue.height).toBeLessThan(board.y);
    expect(cue.width).toBeGreaterThanOrEqual(48);
    await expect(page.getByRole('button', { name: /^Yıldız, konum/ })).toHaveCount(1);
    const bounds = await page.locator('.shape-button').first().boundingBox();
    expect(bounds.width).toBeGreaterThanOrEqual(44); expect(bounds.height).toBeGreaterThanOrEqual(44);
    const last = await page.locator('.shape-button').last().boundingBox();
    await page.screenshot({ path: `../../artifacts/attention-ui/${testInfo.project.name}-level-${level}.png`, fullPage: true });
    expect(last.y + last.height).toBeLessThanOrEqual(page.viewportSize().height);
    await noOverflow(page); expect(writes).toEqual([]); expect(errors).toEqual([]);
  });
}

test('hidden page pauses the trial and requires explicit resume', async ({ page }) => {
  const { errors, writes } = await prepare(page); await pauseClock(page);
  await practice(page); await page.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await page.clock.runFor(100);
  await page.clock.runFor(500);
  await page.evaluate(() => {
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  await page.clock.runFor(20000);
  await expect(page.getByText('Duraklatıldı', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Tur 1 / 24' })).toBeVisible();
  await page.evaluate(() => {
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => false });
    document.dispatchEvent(new Event('visibilitychange'));
  });
  await expect(page.getByRole('button', { name: 'Devam et', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Devam et', exact: true }).click();
  await page.clock.runFor(500); await page.getByRole('button', { name: /^Yıldız, konum/ }).click();
  await page.clock.runFor(100);
  await expect(page.locator('.counters')).toContainText('Doğru1');
  expect(writes).toEqual([]); expect(errors).toEqual([]);
});

for (const role of ['Student', 'Editor', 'InstitutionAdmin']) {
  test(`${role} cannot access prototype directly`, async ({ page }) => {
    await prepare(page, [role]); await expect(page).toHaveURL(/\/error\/403/);
    await expect(page.getByRole('button', { name: 'Alıştırmayı başlat' })).toHaveCount(0);
  });
}

test('mixed Teacher Student roles can access the preview without changing enrolment', async ({ page }) => {
  const { writes, errors } = await prepare(page, ['Teacher', 'Student']);
  await expect(page.getByRole('heading', { name: 'Hedefi Yakala.' })).toBeVisible();
  expect(writes).toEqual([]); expect(errors).toEqual([]);
});

test('keyboard-only practice keeps focus through all three rounds and the start transition', async ({ page }) => {
  const { writes, errors } = await prepare(page); await pauseClock(page);
  const start = page.getByRole('button', { name: 'Alıştırmayı başlat' });
  for (let step = 0; step < 30 && !await start.evaluate(el => el === document.activeElement); step++) {
    await page.keyboard.press('Tab');
  }
  await expect(start).toBeFocused(); await page.keyboard.press('Enter'); await page.clock.runFor(100);
  for (let round = 0; round < 3; round++) {
    await expect(page.locator('.shape-board .shape-button:focus')).toHaveCount(1);
    const star = page.getByRole('button', { name: /^Yıldız, konum/ });
    const targetPosition = Number((await star.getAttribute('aria-label')).match(/\d+/)[0]);
    const focusedPosition = await page.locator('.shape-button:focus').getAttribute('aria-label');
    const direction = Number(focusedPosition.match(/\d+/)[0]) > targetPosition ? 'Shift+Tab' : 'Tab';
    for (let step = 0; step < 6 && !await star.evaluate(el => el === document.activeElement); step++) {
      await page.keyboard.press(direction);
    }
    await expect(star).toBeFocused(); await page.keyboard.press('Enter'); await page.clock.runFor(800);
  }
  await expect(page.getByRole('button', { name: 'Denemeyi başlat' })).toBeFocused();
  await page.keyboard.press('Enter'); await page.clock.runFor(100);
  await expect(page.locator('.shape-button').first()).toBeFocused();
  expect(writes).toEqual([]); expect(errors).toEqual([]);
});

test('catalogue card opens the prototype without replacing the Focus catalogue', async ({ page }) => {
  const { writes, errors } = await prepare(page);
  const typeId = '70000000-0000-0000-0000-000000000002';
  await page.route('**/api/**/exercise-types?*', route => route.fulfill({ json: {
    items: [{ id: typeId, name: 'Focus', displayName: 'Odaklanma', engineType: 'focus', isActive: true }], totalCount: 1
  } }));
  await page.route('**/api/**/exercises?*', route => route.fulfill({ json: {
    items: [{ id: '70000000-0000-0000-0000-000000000001', exerciseTypeId: typeId,
      title: 'Odaklanma', exerciseTypeName: 'Focus', difficultyLevel: 3,
      configurationJson: JSON.stringify({ engineType: 'focus', engineConfig: { mode: 'position', nLevel: 1 } }) }], totalCount: 1
  } }));
  await page.goto('/student/exercises');
  await expect(page.getByRole('heading', { name: 'Odaklanma', exact: true })).toBeVisible();
  const card = page.locator('.exercise-card').filter({ has: page.getByRole('heading', { name: 'Hedefi Yakala', exact: true }) });
  await expect(card).toContainText('Sonuçlar kaydedilmez');
  await page.locator('.category-tabs').getByRole('button', { name: /Odaklanma/ }).click();
  await expect(page.getByRole('heading', { name: 'Odaklanma', exact: true })).toBeVisible();
  await expect(card).toBeVisible();
  await card.getByRole('button', { name: 'Önizlemeyi başlat' }).click();
  await expect(page.getByRole('heading', { name: 'Hedefi Yakala.' })).toBeVisible();
  expect(writes).toEqual([]); expect(errors).toEqual([]);
});

test('all four groups and five levels are selectable and stay selected after reset', async ({ page }) => {
  const { writes, errors } = await prepare(page);
  const groups = ['Çocuk', 'Genç', 'Genç yetişkin', 'Yetişkin'];
  for (const group of groups) {
    await page.getByRole('button', { name: group, exact: true }).click();
    for (let level = 1; level <= 5; level++) {
      await page.getByRole('button', { name: `Seviye ${level}`, exact: true }).click();
      await expect(page.locator('.settings-summary')).toContainText(`${group} · Seviye ${level}`);
      await expect(page.getByRole('button', { name: group, exact: true })).toHaveAttribute('aria-pressed', 'true');
      await expect(page.getByRole('button', { name: `Seviye ${level}`, exact: true })).toHaveAttribute('aria-pressed', 'true');
    }
  }
  await noOverflow(page); await pauseClock(page); await practice(page);
  await page.getByRole('button', { name: 'Denemeyi başlat' }).click();
  await page.clock.runFor(100);
  await expect(page.locator('.age-option')).toHaveCount(0);
  await expect(page.locator('.level-option')).toHaveCount(0);
  for (let round = 0; round < 48; round++) {
    await page.getByRole('button', { name: /^Yıldız, konum/ }).click(); await page.clock.runFor(800);
  }
  await expect(page.getByRole('heading', { name: 'Hedeflerin 48 tanesini yakaladınız.' })).toBeVisible();
  await page.getByRole('button', { name: 'Yeniden dene' }).click();
  await expect(page.locator('.settings-summary')).toContainText('Yetişkin · Seviye 5');
  expect(writes).toEqual([]); expect(errors).toEqual([]);
});
