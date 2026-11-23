import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// Institutions buy maxkoç and Master Hızlı Okuma themselves (user decisions 23.11.2026): students use the products free,
// independent teachers and institutions buy packages; a new student takes a seat, students already theirs stay; no trials.
const root = new URL('../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, root), 'utf8');

test('billing sells teacher and institution plans while students are free', () => {
  assert.match(read('services/billing-service/Billing.Domain/Catalog.cs'), /public bool StaffSeatsRequired \{ get; set; \}/);
  const rules = read('services/billing-service/Billing.Domain/BillingRules.cs');
  assert.match(rules, /requireSubscription \|\| staffSeatsRequired && buyer != BuyerType\.Individual/);
  const orders = read('services/billing-service/Billing.Infrastructure/BillingOrders.cs');
  assert.equal((orders.match(/BillingRules\.Sells\(/g) ?? []).length, 2, 'the order and the plan check use the same rule');
  assert.match(read('services/billing-service/Billing.Infrastructure/BillingCatalog.cs'), /plan\.BuyerType|item\.BuyerType != BuyerType\.Individual/);
  assert.match(read('services/billing-service/Billing.Infrastructure/Migrations/20261123100000_StaffSeats.cs'), /name: "StaffSeatsRequired"/);
  assert.match(read('shared/EduPlatform.Shared.Contracts/Billing/BillingAccessContracts.cs'), /bool StaffSeatsRequired = false\);/);
});

test('a new student takes a seat of the teacher or the institution, checked on inviting and taken on accepting', () => {
  const coaching = read('services/coaching-service/Coaching.Infrastructure/Profiles/CoachingInvitationStore.cs');
  assert.match(coaching, /billingSeats!\.GetPoolAsync\(seatOwner, ct\)/);
  assert.match(coaching, /billingSeats!\.AssignAsync\(seatOwner, actor, invitation\.InviterUserId, ct\)/);
  assert.match(coaching, /Yeni öğrenci eklemek için bir öğretmen paketi seçin\./);
  const speedReading = read('services/speed-reading-service/SpeedReading.API/Controllers/SpeedReadingInvitationsController.cs');
  assert.match(speedReading, /InstitutionSeatNeededAsync\(invitationId, userId, cancellationToken\)/);
  assert.match(speedReading, /ReleaseWhenLeavingAsync\(given, userId, userId, cancellationToken\)/, 'a seat is given back when joining fails');
});

test('the pages sell packages: subscription page, maxkoç home, Hızlı Okuma pricing, admin switch', () => {
  const subscription = read('clients/shared/billing/my-subscription.ts');
  assert.match(subscription, /this\.me\.staffSeatsRequired && \(this\.plans\.length > 0 \|\| this\.subscriptions\.length > 0\)/);
  assert.match(subscription, /'Kurum paketleri' : 'Öğretmen paketleri'/);
  const home = read('clients/admin-panel/src/app/features/coaching-cms/pages/coaching-public-home.component.ts');
  assert.match(home, /readonly packageGroups = computed/);
  assert.match(home, /Kurum olarak kaydol/);
  assert.match(read('clients/speed-reading/src/app/features/public/home/sections/pricing-section/pricing-section.html'), /routerLink="\/auth\/register-school">Kurum olarak kaydol/);
  assert.match(read('clients/admin-panel/src/app/features/billing/billing-products.ts'), /Öğretmen ve kurumlar paket alsın/);
});

// Live test 23.11.2026: with a plan that sells no extra seats, the "full" message must not say "ek öğrenci alın".
test('a full plan without extra seats only suggests a bigger package', () => {
  const coaching = read('services/coaching-service/Coaching.Infrastructure/Profiles/CoachingInvitationStore.cs');
  assert.match(coaching, /pool\.ExtraSeatPrice is null\s*\?\s*forTeacher \? TeacherPlanFullNoExtra : InstitutionPlanFullNoExtra/);
  const speedReading = read('services/speed-reading-service/SpeedReading.API/Controllers/SpeedReadingInvitationsController.cs');
  assert.match(speedReading, /pool\.ExtraSeatPrice is null \? InstitutionPlanFullNoExtra : InstitutionPlanFull/);
  for (const source of [coaching, speedReading]) {
    for (const line of source.split('\n').filter((item) => /PlanFullNoExtra = "/.test(item))) {
      assert.doesNotMatch(line, /ek öğrenci/);
      assert.match(line, /daha büyük bir paket seçin\./);
    }
  }
});
