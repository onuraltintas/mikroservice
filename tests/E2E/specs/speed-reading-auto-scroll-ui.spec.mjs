import { test, expect } from '@playwright/test';

const exerciseId = '60000000-0000-0000-0000-000000000001';
const typeId = '60000000-0000-0000-0000-000000000002';
const textId = '60000000-0000-0000-0000-000000000003';
const content = Array(55).fill('Düzenli okuma dikkatimizi geliştirir ve öğrenmeyi kolaylaştırır.').join(' ');
const variants = [
  { name: 'speed reading', type: 'SpeedReading', engine: 'word_highlight', panel: '.highlight-display-area',
    config: { mode: 'speed_reading', pacer: { speedWpm: 1200, chunkSize: 1 } } },
  { name: 'grouping', type: 'Chunking', engine: 'word_highlight', panel: '.highlight-display-area',
    config: { mode: 'chunking', pacer: { speedWpm: 1200, chunkSize: 2 } } },
  { name: 'fading', type: 'TextFading', engine: 'text_fade', panel: '.text-display-area',
    config: { fading: { speedWpm: 1200, lagMs: 0 } } },
  { name: 'regression', type: 'RegressionReduction', engine: 'regression_reduction', panel: '.regression-text-area',
    config: { wpm: 1200, chunkSize: 2, maskingType: 'trailing' } },
  { name: 'subvocalization', type: 'SubvocalizationReduction', engine: 'subvocalization_reduction', panel: '.subvoc-display-area',
    config: { targetWpm: 1200, chunkSize: 2, displayMode: 'highlight', metronomeEnabled: false } }
];

async function prepare(page, variant, instant = true) {
  const errors = [], writes = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.addInitScript(({ instant }) => {
    localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' }));
    window.__readingScrollEvents = [];
    const scrollTo = Element.prototype.scrollTo;
    Element.prototype.scrollTo = function(options, y) {
      if (!this.matches('.highlight-display-area,.text-display-area,.regression-text-area,.subvoc-display-area.highlight-mode')) {
        return scrollTo.call(this, options, y);
      }
      const active = this.querySelector('.chunk-group.highlight,.fading-word-item.active,.regression-word.active,.subvoc-word.active');
      const bounds = this.getBoundingClientRect(), current = active?.getBoundingClientRect();
      const lineHeight = active ? parseFloat(getComputedStyle(active).lineHeight) + (parseFloat(getComputedStyle(active.parentElement).rowGap) || 0) : 0;
      const top = typeof options === 'object' ? options.top : y;
      window.__readingScrollEvents.push({ from: this.scrollTop, to: top, max: this.scrollHeight - this.clientHeight,
        lineHeight, linesRemaining: current ? (Math.min(innerHeight, bounds.top + this.clientHeight) - current.bottom) / lineHeight : null });
      // Deterministic geometry checks use instant movement; a separate test
      // below exercises the unmodified native smooth animation in real time.
      return scrollTo.call(this, instant ? { top, behavior: 'instant' } : options);
    };
  }, { instant });
  const configuration = { engineType: variant.engine, readingTextId: textId,
    engineConfig: { ...variant.config, readingPurpose: 'practice', content: { minWordCount: 1, maxWordCount: 5000 } } };
  const exercise = { id: exerciseId, exerciseTypeId: typeId, title: 'Kaydırma testi', difficultyLevel: 3,
    exerciseTypeName: variant.type, configurationJson: JSON.stringify(configuration) };
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = { items: [], totalCount: 0 };
    if (path.endsWith('/auth/refresh-token')) {
      const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role: 'Teacher' };
      response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: ['Teacher'] };
    } else if (request.method() !== 'GET') {
      writes.push(path); return route.fulfill({ status: 500, json: { message: 'Unexpected fixture write' } });
    } else if (path.endsWith('/exercise-types')) response = { items: [{ id: typeId, name: variant.type,
      displayName: 'Kaydırma', engineType: variant.engine, isActive: true }], totalCount: 1 };
    else if (path.endsWith(`/exercises/${exerciseId}`)) response = exercise;
    else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1 };
    else if (path.endsWith(`/reading-texts/${textId}`)) response = { id: textId, title: 'Uzun okuma metni', content,
      difficultyLevel: 3, isActive: true, wordCount: content.split(/\s+/).length, questions: [] };
    else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
    else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
    await route.fulfill({ json: response });
  });
  await page.goto('/student/exercises', { waitUntil: 'domcontentloaded' });
  await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
  await expect(page.locator('.start-button-large')).toBeVisible();
  return { errors, writes };
}

for (const variant of variants) test(`${variant.name} advances in three-line blocks without user scrolling`, async ({ page }, testInfo) => {
  const { errors, writes } = await prepare(page, variant);
  await page.clock.install(); await page.clock.pauseAt(new Date());
  await page.locator('.start-button-large').click();
  await page.clock.runFor(100);
  const panel = page.locator(variant.panel);
  const box = await panel.boundingBox();
  expect(box.y + box.height).toBeLessThanOrEqual(page.viewportSize().height - 8);
  expect(await page.evaluate(() => window.__readingScrollEvents.length)).toBe(0);

  let events = [];
  for (let tick = 0; tick < 100 && events.length < 2; tick++) {
    await page.clock.runFor(200);
    events = await page.evaluate(() => window.__readingScrollEvents);
  }
  expect(events.length).toBeGreaterThanOrEqual(2);
  for (const event of events) {
    expect(event.linesRemaining).toBeGreaterThanOrEqual(2.4);
    expect(event.linesRemaining).toBeLessThanOrEqual(3.1);
    expect(event.to - event.from).toBeGreaterThan(2 * event.lineHeight);
    expect(event.to).toBeLessThanOrEqual(event.max + 1);
  }
  const pause = panel.locator('..').getByRole('button', { name: 'pause', exact: true });
  await pause.click();
  const pausedTop = await panel.evaluate(element => element.scrollTop);
  await page.clock.runFor(2000);
  expect(await page.evaluate(() => window.__readingScrollEvents.length)).toBe(events.length);
  expect(await panel.evaluate(element => element.scrollTop)).toBe(pausedTop);
  await page.getByRole('button', { name: 'play_arrow', exact: true }).click();
  await page.clock.runFor(1000);
  await page.screenshot({ path: testInfo.outputPath('automatic-reading-scroll.png'), fullPage: false });
  await page.clock.runFor(90000);
  await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
  const completedEvents = await page.evaluate(() => window.__readingScrollEvents);
  expect(completedEvents.at(-1).to).toBeCloseTo(completedEvents.at(-1).max, 0);
  expect(errors).toEqual([]); expect(writes).toEqual([]);
});

test('native smooth scrolling follows timer updates without any extra click', async ({ page }) => {
  const { errors, writes } = await prepare(page, variants[0], false);
  await page.locator('.start-button-large').click();
  const panel = page.locator(variants[0].panel);
  await expect.poll(() => panel.evaluate(element => element.scrollTop), { timeout: 15000 }).toBeGreaterThan(100);
  const active = panel.locator('.chunk-group.highlight');
  const panelBox = await panel.boundingBox(), activeBox = await active.boundingBox();
  expect(activeBox.y).toBeGreaterThanOrEqual(panelBox.y - 1);
  expect(activeBox.y + activeBox.height).toBeLessThanOrEqual(Math.min(page.viewportSize().height, panelBox.y + panelBox.height) + 1);
  expect(errors).toEqual([]); expect(writes).toEqual([]);
});
