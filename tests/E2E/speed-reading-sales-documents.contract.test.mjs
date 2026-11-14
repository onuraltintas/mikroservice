import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

// Legal check Y1 (user decision 14.11.2026): Hızlı Okuma purchases get the pre-information form, the distance-sales
// contract, their acceptance and the withdrawal (cayma) request, as Koçluk has. Y9: the 18+ payer label leaves the admin tables.
const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const service = 'services/speed-reading-service';

test('the documents are versioned texts with a seller block, the price and a 14-day full-refund withdrawal', async () => {
  const texts = await read(`${service}/SpeedReading.Application/Subscription/SpeedReadingSalesDocuments.cs`);
  assert.match(texts, /public const string Version = "2026-11-14";/);
  assert.match(texts, /public const int WithdrawalDays = 14;/);
  for (const heading of ['ÖN BİLGİLENDİRME FORMU', 'MESAFELİ SATIŞ SÖZLEŞMESİ', 'Madde 5 – Cayma hakkı', 'Madde 8 – Uyuşmazlık'])
    assert.ok(texts.includes(heading), heading);
  assert.match(texts, /hizmetin kullanıldığı günler için kesinti yapılmaz/);
  assert.match(texts, /6502 sayılı Tüketicinin Korunması Hakkında Kanun/);
  assert.doesNotMatch(texts, /girilmedi|taslak|test dönemi|18 yaş/i);
  assert.match(texts, /public bool IsComplete =>/);
});

test('a payment notice is refused without a complete seller and the accepted version, and keeps what was accepted', async () => {
  const subscription = await read(`${service}/SpeedReading.Infrastructure/Legacy/LegacySpeedReadingSubscription.cs`);
  assert.match(subscription, /"Sales\.SellerMissing"/);
  assert.match(subscription, /request\.SalesDocumentsVersion\?\.Trim\(\), SpeedReadingSalesDocumentTexts\.Version/);
  assert.match(subscription, /"Sales\.DocumentsNotAccepted"/);
  assert.match(subscription, /SalesDocumentsJson = JsonSerializer\.Serialize\(documents\)/);
  assert.match(subscription, /approvedAt\.AddDays\(SpeedReadingSalesDocumentTexts\.WithdrawalDays\)/);
});

test('withdrawal is the buyer\'s own, inside 14 days, and an approval ends the access but keeps the payment', async () => {
  const sales = await read(`${service}/SpeedReading.Infrastructure/Legacy/SpeedReadingSalesService.cs`);
  assert.match(sales, /row\.Value\.Request\.UserId != userId\) return null/);
  for (const code of ['Sales.NotWithdrawable', 'Sales.WithdrawalExists', 'Sales.WithdrawalPeriodOver', 'Sales.WithdrawalNoteRequired'])
    assert.ok(sales.includes(`"${code}"`), code);
  assert.match(sales, /subscription\.Status = "Cancelled";/);
  assert.doesNotMatch(sales, /request\.Status = /);
  assert.match(sales, /item\.Id == requestId && item\.UserId == userId/);
});

test('the endpoints: seller and withdrawal review need the subscription permission, the rest a signed-in buyer', async () => {
  const controller = await read(`${service}/SpeedReading.API/Controllers/BankTransferPaymentsController.cs`);
  for (const route of ['"seller"', '"requests/{id:guid}/withdrawal/review"']) {
    const at = controller.indexOf(`(${route})`);
    assert.ok(at > 0, route);
    assert.match(controller.slice(at, at + 200), /HasPermission\(PlatformPermissions\.SpeedReading\.SubscriptionManage\)/, route);
  }
  for (const route of ['"documents"', '"requests/{id:guid}/documents"', '"requests/{id:guid}/withdrawal"']) {
    const at = controller.indexOf(`(${route})`);
    assert.ok(at > 0, route);
    const head = controller.slice(at, at + 120);
    assert.match(head, /\[Authorize\]/, route);
    assert.doesNotMatch(head, /AllowAnonymous|HasPermission/, route);
  }
});

test('the migration adds only nullable columns and the snapshot knows them', async () => {
  const migration = await read(`${service}/SpeedReading.Infrastructure/Persistence/Migrations/20261114190000_SalesDocumentsAndWithdrawal.cs`);
  const snapshot = await read(`${service}/SpeedReading.Infrastructure/Persistence/Migrations/OwnedSpeedReadingDbContextModelSnapshot.cs`);
  assert.match(migration, /\[Migration\("20261114190000_SalesDocumentsAndWithdrawal"\)\]/);
  assert.doesNotMatch(migration, /nullable: false|Sql\(/);
  const columns = ['SellerTitle', 'SellerAddress', 'SellerTaxOffice', 'SellerTaxNumber', 'SellerMersisNumber', 'SellerEmail', 'SellerPhone',
    'SalesDocumentsVersion', 'SalesDocumentsJson', 'SalesDocumentsAcceptedAt', 'WithdrawalStatus', 'WithdrawalReason', 'WithdrawalRequestedAt',
    'WithdrawalReviewNote', 'WithdrawalReviewedAt', 'WithdrawalReviewedBy'];
  for (const column of columns) {
    assert.ok(migration.includes(`"${column}"`), `migration ${column}`);
    assert.ok(snapshot.includes(`("${column}")`), `snapshot ${column}`);
  }
});

test('the payment page shows both documents, asks for acceptance and offers withdrawal', async () => {
  const html = await read('clients/speed-reading/src/app/features/public/payment/payment.component.html');
  const component = await read('clients/speed-reading/src/app/features/public/payment/payment.component.ts');
  for (const id of ['sales-documents', 'accept-documents', 'documents-error', 'open-withdrawal', 'withdrawal-form', 'submit-withdrawal', 'show-accepted-documents'])
    assert.ok(html.includes(`data-testid="${id}"`), id);
  assert.match(html, /\[disabled\]="submitting\(\) \|\| !documents\(\)"/);
  assert.match(component, /salesDocumentsVersion: documents\.version/);
  assert.match(component, /this\.documentsAccepted = false;/);
});

test('the admin pages: seller form and withdrawal review for Hızlı Okuma, no 18+ payer label anywhere', async () => {
  const speed = await read('clients/admin-panel/src/app/features/dashboard/pages/speed-reading-subscriptions.ts');
  const coaching = await read('clients/admin-panel/src/app/features/dashboard/pages/coaching-subscriptions.ts');
  assert.match(speed, /data-testid="seller-form"/);
  assert.match(speed, /approveWithdrawal\(request\)/);
  assert.match(speed, /rejectWithdrawal\(request\)/);
  for (const page of [speed, coaching]) assert.doesNotMatch(page, /adultPayerLabel|18\+ ödeme beyanı|Yetişkin beyanı/);
});
