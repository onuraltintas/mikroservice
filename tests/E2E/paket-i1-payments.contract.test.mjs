// Paket I1 (live user test 13.11.2026): payments, withdrawal and sales documents. Source-level checks that hold the
// fixes in place next to the unit and integration tests.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');

test('a withdrawal approval needs no note and promises the whole amount', async () => {
  const service = await read('services/coaching-service/Coaching.Infrastructure/Management/CoachingSalesService.cs');
  assert.match(service, /if \(text is null && !approve\) throw new ArgumentException/);
  assert.match(service, /text \?\?= ApprovedWithdrawalNote;/);
  const admin = await read('clients/admin-panel/src/app/features/dashboard/pages/coaching-sales.ts');
  assert.match(admin, /title: 'İade notu'/);
  assert.match(admin, /tutarın tamamını 14 gün içinde siz iade edersiniz/);
  for (const path of ['clients/admin-panel/src/app/features/dashboard/pages/coaching-sales.ts', 'clients/admin-panel/shared/coaching-my-payments.component.ts'])
    assert.doesNotMatch(await read(path), /günler(i)? düş/, `${path} still deducts the used days`);
});

test('the buyer never reads the admin\'s own notes or id', async () => {
  const service = await read('services/coaching-service/Coaching.Infrastructure/Management/CoachingSubscriptionService.cs');
  assert.match(service, /internal static CoachingBankTransferRequestSummary\? ForBuyer\(/);
  // Every buyer-facing answer (the payer's and the parent's lists, create, update, withdraw) goes through ForBuyer.
  const buyerPart = service.slice(service.indexOf('GetChildBankTransferRequestsAsync'), service.indexOf('public async Task<PagedResponse<CoachingBankTransferRequestSummary>> GetBankTransferRequestsAsync'));
  assert.ok(buyerPart.length > 1000);
  for (const line of buyerPart.split('\n').filter(line => /return .*(GetBankTransferSummaryAsync|ToTransferSummary)\(/.test(line)))
    assert.match(line, /ForBuyer\(/, line.trim());
  const sales = await read('services/coaching-service/Coaching.Infrastructure/Management/CoachingSalesService.cs');
  assert.match(sales, /WithdrawalStatus == "Rejected" \? x\.transfer\.WithdrawalReviewNote : null/);
  const payments = await read('clients/admin-panel/shared/coaching-my-payments.component.ts');
  assert.match(payments, /withdrawal-note-/);
  const student = await read('clients/admin-panel/src/app/features/coaching-portal/pages/coaching-subscription.component.ts');
  assert.doesNotMatch(student, /İnceleme notu/);
});

test('the buyer\'s name comes from the account directory when the token has none', async () => {
  const helper = await read('services/coaching-service/Coaching.API/Security/CoachingBuyerName.cs');
  assert.match(helper, /ICoachingAccountDirectory/);
  for (const path of ['services/coaching-service/Coaching.API/Controllers/CoachingSubscriptionsController.cs',
    'services/coaching-service/Coaching.API/Controllers/CoachingSalesController.cs']) {
    const controller = await read(path);
    assert.match(controller, /CoachingBuyerName\.ResolveAsync\(HttpContext/);
    assert.doesNotMatch(controller, /FindFirstValue\(ClaimTypes\.Name\)/, `${path} still reads the name only from the token`);
  }
});

test('no payment notice is offered while the seller details are missing', async () => {
  const contracts = await read('services/coaching-service/Coaching.Application/Subscriptions/CoachingSubscriptionContracts.cs');
  assert.match(contracts, /bool SellerInfoComplete = false\);/);
  const checkout = await read('clients/admin-panel/shared/coaching-checkout.component.ts');
  assert.match(checkout, /data-testid="seller-not-ready"/);
  for (const path of ['clients/admin-panel/src/app/features/coaching-portal/pages/coaching-subscription.component.ts',
    'clients/admin-panel/src/app/features/coaching-portal/parent/parent-payments.component.ts',
    'clients/admin-panel/projects/staff-portal/src/app/features/coaching/coaching-teacher-subscription.component.ts'])
    assert.match(await read(path), /\[sellerReady\]="settings\(\)\?\.sellerInfoComplete !== false"/, path);
  assert.match(await read('clients/admin-panel/src/app/features/dashboard/pages/coaching-sales.ts'), /data-testid="seller-missing"/);
});

test('admin dialogs name the payer and the license list shows no raw id', async () => {
  const subscriptions = await read('clients/admin-panel/src/app/features/dashboard/pages/coaching-subscriptions.ts');
  assert.doesNotMatch(subscriptions, /kullanıcısının/);
  assert.match(subscriptions, /yalnız yöneticiler görür/);
  const licenses = await read('clients/admin-panel/src/app/features/dashboard/pages/coaching-license-tools.ts');
  assert.doesNotMatch(licenses, /row\.institutionId \|\| row\.userId/);
});
