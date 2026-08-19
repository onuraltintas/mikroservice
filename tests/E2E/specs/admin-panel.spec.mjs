import { test, expect } from '@playwright/test';

test.describe('Admin panel critical navigation', () => {
  test.skip(
    process.env.E2E_RUN_UI !== 'true' && process.env.E2E_START_UI !== 'true',
    'Set E2E_START_UI=true for local Angular or E2E_UI_BASE_URL/E2E_RUN_UI=true for staging UI tests.'
  );

  test('renders the public login form', async ({ page }) => {
    await page.goto('/auth/login');
    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('input[name="password"]')).toBeVisible();
    await expect(page.getByRole('button', { name: /Giriş Yap/i })).toBeVisible();
  });

  test('shows the login form and reaches the protected dashboard after login', async ({ page }) => {
    test.skip(
      process.env.E2E_RUN_UI !== 'true' ||
        !process.env.E2E_ADMIN_EMAIL ||
        !process.env.E2E_ADMIN_PASSWORD,
      'Set E2E_RUN_UI=true, E2E_ADMIN_EMAIL and E2E_ADMIN_PASSWORD to run the browser journey.'
    );

    await page.goto('/auth/login');
    await expect(page.locator('input[name="email"]')).toBeVisible();
    await expect(page.locator('input[name="password"]')).toBeVisible();

    await page.locator('input[name="email"]').fill(process.env.E2E_ADMIN_EMAIL);
    await page.locator('input[name="password"]').fill(process.env.E2E_ADMIN_PASSWORD);
    await page.getByRole('button', { name: /Giriş Yap/i }).click();

    await expect(page).toHaveURL(/\/dashboard(?:\/)?$/, { timeout: 30_000 });
    await expect(page.locator('body')).not.toContainText('Giriş başarısız');
  });
});
