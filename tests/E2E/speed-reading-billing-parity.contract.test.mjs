import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { test } from 'node:test';

// Hızlı Okuma billing parity (user decision 14.11.2026): the subscription works as it does in Koçluk — payment and
// withdrawal decisions and expiry reach the payer (in-app and by e-mail) and the admin, an approved payment carries its
// invoice, and the subscriber has a page of their own inside the app.
const read = path => readFile(new URL(`../../${path}`, import.meta.url), 'utf8');
const service = 'services/speed-reading-service';

test('one contract carries every notice kind, with words for each', async () => {
  const events = await read('shared/EduPlatform.Shared.Contracts/Events/SpeedReading/SpeedReadingEvents.cs');
  const kinds = ['TransferSubmitted', 'TransferApproved', 'TransferRejected', 'WithdrawalRequested', 'WithdrawalApproved', 'WithdrawalRejected', 'Expiring', 'Expired'];
  for (const kind of kinds) {
    assert.match(events, new RegExp(`public const string ${kind} = "${kind}";`), kind);
    assert.match(events, new RegExp(`SpeedReadingSubscriptionNoticeKinds\\.${kind} => new\\(`), `${kind} words`);
  }
  assert.match(events, /IsForAdmin\(string kind\) => kind is TransferSubmitted or WithdrawalRequested;/);
});

test('notices are staged with the operation that causes them', async () => {
  const subscription = await read(`${service}/SpeedReading.Infrastructure/Legacy/LegacySpeedReadingSubscription.cs`);
  const sales = await read(`${service}/SpeedReading.Infrastructure/Legacy/SpeedReadingSalesService.cs`);
  for (const kind of ['TransferSubmitted', 'TransferApproved', 'TransferRejected']) assert.ok(subscription.includes(`SpeedReadingSubscriptionNoticeKinds.${kind}`), kind);
  for (const kind of ['WithdrawalRequested', 'WithdrawalApproved', 'WithdrawalRejected']) assert.ok(sales.includes(`SpeedReadingSubscriptionNoticeKinds.${kind}`), kind);
  // Staged before the save, so a failed operation tells nobody.
  const create = subscription.indexOf('SpeedReadingSubscriptionNoticeKinds.TransferSubmitted');
  assert.ok(create < subscription.indexOf('OwnedContentMutationIdempotency.SaveAsync(ownedDb, BankTransferRequestScope', create));
  const notices = await read(`${service}/SpeedReading.Infrastructure/Legacy/SpeedReadingSubscriptionNotices.cs`);
  assert.match(notices, /db\.Notifications\.Add\(new LegacyUserNotification/);
  assert.match(notices, /ActionUrl = SubscriptionPage/);
  assert.match(notices, /publishEndpoint\.Publish\(new SpeedReadingSubscriptionNoticeV1\(/);
  assert.doesNotMatch(notices, /SaveChanges/);
});

test('reminders: individual subscriptions only, once per stage, and only while a renewal can be bought', async () => {
  const lifecycle = await read(`${service}/SpeedReading.Infrastructure/Legacy/SpeedReadingSubscriptionLifecycle.cs`);
  assert.match(lifecycle, /!SpeedReadingSalesService\.Seller\(settings\)\.IsComplete\)\s*return 0;/);
  assert.match(lifecycle, /subscription\.InstitutionAccessLicenseId == null/);
  assert.match(lifecycle, /pg_advisory_xact_lock\(hashtextextended\('speed-reading-subscription-lifecycle', 0\)\)/);
  assert.match(lifecycle, /"expired" : end - now <= TimeSpan\.FromDays\(1\) \? "1d" : "7d"/);
  assert.match(lifecycle, /LegacySubscriptionNoticeReceipt/);
  assert.doesNotMatch(lifecycle, /subscription\.Status = /);
  const program = await read(`${service}/SpeedReading.API/Program.cs`);
  assert.match(program, /AddHostedService<SpeedReading\.Infrastructure\.Legacy\.SpeedReadingSubscriptionLifecycleWorker>\(\)/);
});

test('the notification service e-mails the payer and the admin from its own queue', async () => {
  const consumer = await read('services/notification-service/Notification.Application/Consumers/SpeedReadingSubscriptionNoticeConsumer.cs');
  const endpoints = await read('services/notification-service/Notification.API/Messaging/NotificationReceiveEndpoints.cs');
  assert.match(endpoints, /new\("speed-reading-subscription-notice", typeof\(SpeedReadingSubscriptionNoticeConsumer\)\)/);
  assert.match(consumer, /SubscriptionPath = "\/student\/subscription"/);
  assert.match(consumer, /AdminPath = "\/dashboard\/speed-reading\/subscriptions"/);
  assert.match(consumer, /EmailContent\.Html\(message\)/);
  const routes = await read('clients/speed-reading/src/app/features/student/student.routes.ts');
  assert.match(routes, /path: 'subscription',\s*canActivate: \[authGuard\],/);
});

test('invoices: a PDF on an approved payment, admin records it, only the buyer or an admin opens it', async () => {
  const sales = await read(`${service}/SpeedReading.Infrastructure/Legacy/SpeedReadingSalesService.cs`);
  assert.match(sales, /SequenceEqual\("%PDF-"u8\)/);
  assert.match(sales, /MaxInvoiceBytes = 10 \* 1024 \* 1024/);
  for (const code of ['Sales.InvoiceInvalid', 'Sales.InvoiceNotAllowed', 'Sales.InvoiceExists']) assert.ok(sales.includes(`"${code}"`), code);
  assert.match(sales, /!asAdmin && !await db\.BankTransferPaymentRequests\.AsNoTracking\(\)\s*\.AnyAsync\(item => item\.Id == requestId && item\.UserId == viewerId/);
  const controller = await read(`${service}/SpeedReading.API/Controllers/BankTransferPaymentsController.cs`);
  for (const route of ['[HttpPost("requests/{id:guid}/invoice")]', '[HttpDelete("requests/{id:guid}/invoice")]', '[HttpGet("requests/{id:guid}/invoice/admin")]']) {
    const at = controller.indexOf(route);
    assert.ok(at > 0, route);
    assert.match(controller.slice(at, at + 160), /HasPermission\(PlatformPermissions\.SpeedReading\.SubscriptionManage\)/, route);
  }
  const own = controller.indexOf('[HttpGet("requests/{id:guid}/invoice")]');
  assert.ok(own > 0);
  assert.doesNotMatch(controller.slice(own, own + 90), /HasPermission|AllowAnonymous/);
  assert.match(controller.slice(own, own + 400), /asAdmin: false/);
});

test('the contract says where the invoice and the withdrawal are, and that is where they are', async () => {
  const texts = await read(`${service}/SpeedReading.Application/Subscription/SpeedReadingSalesDocuments.cs`);
  assert.match(texts, /const string withdrawalPlace = "Aboneliğim";/);
  assert.match(texts, /sayfasından indirilebilir\./);
  assert.doesNotMatch(texts, /e-posta adresine gönderilir/);
  const page = await read('clients/speed-reading/src/app/features/student/subscription/subscription-page.component.html');
  assert.match(page, /<h1>Aboneliğim<\/h1>/);
  assert.match(page, /<app-payment-requests /);
  const list = await read('clients/speed-reading/src/app/features/public/payment/payment-requests.component.html');
  assert.match(list, /data-testid="download-invoice"/);
  const shell = await read('clients/speed-reading/src/app/features/student/shell/student-shell.component.html');
  assert.match(shell, /routerLink="\/student\/subscription"[\s\S]{0,500}Aboneliğim/);
});

test('the migration adds two tables and two nullable columns, and the snapshot knows them', async () => {
  const migration = await read(`${service}/SpeedReading.Infrastructure/Persistence/Migrations/20261114210000_SubscriptionInvoicesAndReminders.cs`);
  const snapshot = await read(`${service}/SpeedReading.Infrastructure/Persistence/Migrations/OwnedSpeedReadingDbContextModelSnapshot.cs`);
  assert.match(migration, /\[Migration\("20261114210000_SubscriptionInvoicesAndReminders"\)\]/);
  for (const name of ['subscription_invoices', 'subscription_notice_receipts', 'InvoiceNumber', 'InvoiceIssuedOn']) {
    assert.ok(migration.includes(`"${name}"`), `migration ${name}`);
    assert.ok(snapshot.includes(`"${name}"`), `snapshot ${name}`);
  }
  assert.doesNotMatch(migration, /Sql\(/);
});

test('the admin page records, downloads and removes the invoice', async () => {
  const page = await read('clients/admin-panel/src/app/features/dashboard/pages/speed-reading-subscriptions.ts');
  assert.match(page, /data-testid="invoice-form"/);
  for (const method of ['startInvoice(request)', 'downloadInvoice(request)', 'removeInvoice(request)']) assert.ok(page.includes(method), method);
});
