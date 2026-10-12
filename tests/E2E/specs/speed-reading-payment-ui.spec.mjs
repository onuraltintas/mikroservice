import { test, expect } from '@playwright/test';

async function mockPaymentApi(page, authenticated) {
  const banks = [], writes = [];
  if (authenticated) {
    await page.addInitScript(() => localStorage.setItem('currentUser', JSON.stringify({ id: 'eft-test-user' })));
  }
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let response = [];
    if (path.endsWith('/auth/refresh-token')) {
      response = { id: 'eft-test-user', roles: ['Student'],
        token: `e30.${Buffer.from(JSON.stringify({ sub: 'eft-test-user', exp: Math.floor(Date.now() / 1000) + 3600 })).toString('base64url')}.fixture` };
    } else if (request.method() !== 'GET') {
      writes.push(path);
      return route.fulfill({ status: 500, json: {} });
    } else if (path.endsWith('/subscription-plans')) {
      response = [{ id: 'plan-1', name: 'Test yıllık plan', price: 499, durationDays: 365, isContactOnly: false, features: [] }];
    } else if (path.endsWith('/bank-transfer')) {
      banks.push(path);
      response = { accountHolder: 'Test hesabı', bankName: 'Test bankası', iban: 'TEST-IBAN' };
    } else if (path.endsWith('/bank-transfer/requests/my')) {
      response = [];
    }
    await route.fulfill({ json: response });
  });
  return { banks, writes };
}

test('anonymous EFT link requires login and retains the selected plan', async ({ page }) => {
  const { banks, writes } = await mockPaymentApi(page, false);
  await page.goto('/odeme?plan=plan-1');
  await expect(page.getByRole('heading', { name: 'Giriş yapın', exact: true })).toBeVisible();
  const url = new URL(page.url());
  expect(url.pathname).toBe('/auth/login');
  expect(url.searchParams.get('returnUrl')).toBe('/odeme?plan=plan-1');
  await expect(page.locator('.bank-details')).toHaveCount(0);
  await expect(page.locator('input[name="paymentReference"]')).toHaveCount(0);
  expect(banks).toEqual([]);
  expect(writes).toEqual([]);
});

test('signed-in user can view the selected plans bank details and EFT form', async ({ page }) => {
  const { banks, writes } = await mockPaymentApi(page, true);
  await page.goto('/odeme?plan=plan-1');
  await expect(page.locator('.bank-details')).toContainText('Test bankası');
  await expect(page.locator('input[name="paymentReference"]')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Ödeme talebini ilet', exact: true })).toBeDisabled();
  await page.locator('input[name="adultPayerDeclaration"]').check();
  await expect(page.getByRole('button', { name: 'Ödeme talebini ilet', exact: true })).toBeEnabled();
  expect(new URL(page.url()).searchParams.get('plan')).toBe('plan-1');
  expect(banks).toHaveLength(1);
  expect(writes).toEqual([]);
});
