import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';

const read = name => JSON.parse(readFileSync(new URL(`../../../content-packs/comprehension/v1/${name}`, import.meta.url), 'utf8'));
const settings = read('settings.json');
const content = read('content.json');
const typeId = '10000000-0000-0000-0000-000000000002';

// Real Angular UI and canonical content; Identity/API replies are fixtures.
// Actual selection, answer ownership and persistence are separately verified in PostgreSQL.
for (const [index, setting] of settings.entries()) {
  test(`${setting.age} S${setting.level} ${setting.configuration.engineConfig.comprehensionMode}: canonical text and questions`, async ({ page }) => {
    const text = content.filter(row => row.exerciseId === setting.id)[index % 2];
    const config = { ...setting.configuration, readingTextId: text.id };
    const exercise = { id: setting.id, exerciseTypeId: typeId, title: setting.title,
      targetAgeGroupId: setting.ageGroupId, difficultyLevel: setting.level, configurationJson: JSON.stringify(config) };
    const writes = [], errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'test-user' })));
    await page.route('**/api/**', async route => {
      const request = route.request();
      const path = new URL(request.url()).pathname;
      let response = { items: [], totalCount: 0 };
      if (path.endsWith('/auth/refresh-token')) {
        const claims = { sub: 'test-user', exp: Math.floor(Date.now() / 1000) + 3600, role: 'Admin' };
        response = { id: 'test-user', token: `e30.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.fixture`, roles: ['Admin'] };
      } else if (request.method() !== 'GET') {
        writes.push(path);
        return route.fulfill({ status: 500, json: { message: 'Unexpected preview write' } });
      } else if (path.endsWith('/exercise-types')) response = {
        items: [{ id: typeId, name: 'Comprehension', displayName: 'Anlama', engineType: 'reading_comprehension', isActive: true }], totalCount: 1
      };
      else if (path.endsWith(`/exercises/${setting.id}`)) response = exercise;
      else if (path.endsWith('/exercises')) response = { items: [exercise], totalCount: 1, pageNumber: 1, pageSize: 100 };
      else if (path.endsWith(`/reading-texts/${text.id}`)) response = { ...text,
        difficultyLevel: text.level, targetAgeGroupConfigurationId: text.ageGroupId, language: 'tr', isActive: true,
        questions: text.questions.map(({ evidence, ...q }) => q) };
      else if (path.includes('/profile/status')) response = { hasAgeGroupConfiguration: true };
      else if (path.endsWith('/my-modules')) response = { hasSpeedReading: true };
      await route.fulfill({ json: response });
    });
    await page.goto('/student/exercises');
    await page.getByRole('button', { name: 'Özel ayarlarla dene' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Denemeyi başlat' }).click();
    await page.locator('.start-button-large').click();
    await expect(page.locator('.reading-text-content')).toHaveText(text.content);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.locator('.complete-reading-btn').click();
    const learning = config.engineConfig.comprehensionMode === 'learning';
    await expect(page.getByRole('button', { name: 'Metne tekrar bak' })).toHaveCount(learning ? 1 : 0);
    for (const [order, q] of text.questions.entries()) {
      await expect(page.locator('.question-text')).toHaveText(q.questionText);
      await page.locator('.answer-option').nth('ABCD'.indexOf(q.correctAnswer)).click();
      if (learning) await expect(page.locator('.question-card [role="status"]')).toContainText(q.explanation);
      else await expect(page.locator('.answer-option.correct, .answer-option.wrong')).toHaveCount(0);
      await page.getByRole('button', { name: order === text.questions.length - 1 ? 'Sonuçları Gör' : 'Sonraki Soru' }).click();
    }
    await expect(page.locator('.completed-state')).toContainText(learning ? 'Anlama çalışması' : 'Anlama değerlendirmesi');
    await expect(page.locator('.answers-summary')).toContainText(text.questions.at(-1).explanation);
    await expect(page.getByText('Önizleme sonucu — kaydedilmedi.', { exact: true })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    expect(writes).toEqual([]);
    expect(errors).toEqual([]);
  });
}
