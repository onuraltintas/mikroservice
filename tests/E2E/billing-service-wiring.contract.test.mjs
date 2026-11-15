import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// Billing ("Abonelik ve Ödeme") is built in stages and goes live in one release at the end (decision 14.11.2026).
// Until then it must stay out of every environment that does not use its overlay.
const root = new URL('../../', import.meta.url);
const read = file => readFileSync(new URL(file, root), 'utf8');

test('the gateway has a billing cluster, a public catalog route and a signed-in route for the rest', () => {
  const proxy = JSON.parse(read('services/api-gateway/appsettings.json')).ReverseProxy;
  assert.equal(proxy.Clusters['billing-cluster']?.Destinations.destination1.Address, 'http://localhost:5006');
  assert.deepEqual(proxy.Routes['billing-route'], { ClusterId: 'billing-cluster', Match: { Path: '/api/billing/{**catch-all}' } });
  assert.deepEqual(proxy.Routes['billing-catalog-public-route'],
    { ClusterId: 'billing-cluster', AuthorizationPolicy: 'Anonymous', Match: { Path: '/api/billing/v1/catalog/{**catch-all}' } });
  const anonymous = Object.entries(proxy.Routes)
    .filter(([, route]) => route.ClusterId === 'billing-cluster' && route.AuthorizationPolicy === 'Anonymous').map(([name]) => name);
  assert.deepEqual(anonymous, ['billing-catalog-public-route'], 'Only the catalog is open to visitors.');
});

test('the gateway loads billing only when Billing:Enabled is true', () => {
  const routing = read('services/api-gateway/Configuration/OptionalProductRouting.cs');
  assert.match(routing, /new\("Billing:Enabled", "billing-cluster", "\/api\/billing"\)/);
  assert.match(routing, /GetValue\(product\.EnabledKey, false\)/, 'An optional product is off unless switched on.');
  const program = read('services/api-gateway/Program.cs');
  assert.match(program, /LoadFromConfig\(OptionalProductRouting\.ReverseProxySection\(builder\.Configuration\)\)/);
  assert.match(program, /OptionalProductRouting\.VisibleClusterIds\(configuration\)/);
});

test('the service keeps the admin section for the system admin and opens only health and the catalog', () => {
  const program = read('services/billing-service/Billing.API/Program.cs');
  assert.match(program, /AddPolicy\(BillingPolicies\.Admin, policy => policy\.RequireAuthenticatedUser\(\)\s*\.RequireRole\("SystemAdmin"\)/);
  assert.match(program, /--migrate-only/);
  const endpoints = read('services/billing-service/Billing.API/CatalogEndpoints.cs');
  assert.equal((endpoints.match(/AllowAnonymous\(\)/g) ?? []).length, 1);
  assert.match(endpoints, /MapGet\("\/api\/billing\/v1\/catalog\/\{productCode\}"[\s\S]*?\.AllowAnonymous\(\);/);
  assert.match(endpoints, /MapGroup\("\/api\/billing\/v1\/admin"\)\.RequireAuthorization\(BillingPolicies\.Admin\)/);
  const mapped = [...endpoints.matchAll(/\b(app|admin)\.Map(Get|Post|Put|Delete)\(/g)].map(match => match[1]);
  assert.equal(mapped.filter(owner => owner === 'app').length, 1, 'Every endpoint but the catalog hangs off the admin group.');
});

test('billing runs only through its own compose overlays', () => {
  for (const file of ['docker-compose.yml', 'docker-compose.production.yml', 'docker-compose.staging.yml'])
    assert.doesNotMatch(read(file), /billing/i, `${file}: Billing stays in its own overlay.`);
  // Release preparation (15.11.2026): the image is built with the others and the production stack runs with the overlay.
  assert.match(read('.github/workflows/production-images.yml'), /name: billing-service, context: \., dockerfile: services\/billing-service\/Dockerfile/);
  const pull = read('infrastructure/scripts/pull-release-images.sh');
  assert.match(pull, /services="[^"]*\bbilling-service\b/);
  assert.match(pull, /-f docker-compose\.billing\.production\.yml pull/);
  const unit = read('infrastructure/systemd/eduivme-production.service');
  assert.equal(unit.match(/-f \/opt\/eduivme\/docker-compose\.billing\.production\.yml /g)?.length, 3, 'config, up and stop all use the overlay');

  for (const file of ['docker-compose.billing.yml', 'docker-compose.billing.production.yml']) {
    const overlay = read(file);
    assert.match(overlay, /billing-migrations:[\s\S]*?command: \["--migrate-only"\]/, file);
    assert.match(overlay, /billing-service:[\s\S]*?billing-migrations:\s*\n\s+condition: service_completed_successfully/, file);
    assert.match(overlay, /Billing__Enabled[:=]\s*"?true/, file);
    assert.match(overlay, /ReverseProxy__Clusters__billing-cluster__Destinations__destination1__Address[:=]\s*http:\/\/billing-service:8080/, file);
    assert.match(overlay, /dockerfile: services\/billing-service\/Dockerfile/, file);
  }
  const production = read('docker-compose.billing.production.yml');
  assert.match(production, /BILLING_CONNECTION_STRING:\s*\$\{BILLING_CONNECTION_STRING:\?/);
  assert.match(production, /\/billing-service:\$\{RELEASE_TAG:\?/);
  assert.doesNotMatch(production, /ports:/, 'The production service is reachable only through the gateway.');
});

test('the image runs as the non-root app user and CI builds, tests and checks it', () => {
  const dockerfile = read('services/billing-service/Dockerfile');
  assert.match(dockerfile, /\nUSER app\n/);
  assert.match(dockerfile, /ENTRYPOINT \["dotnet", "Billing\.API\.dll"\]/);
  const workflow = read('.github/workflows/ci.yml');
  assert.match(workflow, /- name: billing\s+project: services\/billing-service\/Billing\.API\/Billing\.API\.csproj/);
  assert.match(workflow, /- name: billing\s+context: \.\s+dockerfile: services\/billing-service\/Dockerfile/);
  assert.match(workflow, /dotnet test tests\/Integration\/Billing\.IntegrationTests\/Billing\.IntegrationTests\.csproj/);
  assert.match(workflow, /has-pending-model-changes \\\s+--project services\/billing-service\/Billing\.Infrastructure\/Billing\.Infrastructure\.csproj/);
  assert.match(workflow, /Validate the optional Billing overlays/);
});

test('buyers reach only their own orders, reviews are the admin\'s, and the entitlement query stays inside the network', () => {
  const endpoints = read('services/billing-service/Billing.API/OrderEndpoints.cs');
  assert.match(endpoints, /MapGroup\("\/api\/billing\/v1"\)\.RequireAuthorization\(BillingPolicies\.User\)/);
  assert.match(endpoints, /MapGroup\("\/api\/billing\/v1\/admin"\)\.RequireAuthorization\(BillingPolicies\.Admin\)/);
  for (const review of ['approve', 'reject', 'reverse'])
    assert.match(endpoints, new RegExp(`admin\\.MapPost\\("/orders/\\{id:guid\\}/${review}"`), `${review} is an admin endpoint`);
  // The gateway routes /api/billing only; the internal path is outside it and asks for the shared service key.
  assert.match(endpoints, /app\.MapGet\("\/internal\/billing\/v1\/entitlements"[\s\S]*?InternalServiceAuthentication\.IsValid\(request, configuration\)[\s\S]*?Results\.Unauthorized\(\)/);
  assert.equal((endpoints.match(/\bapp\.Map(Get|Post|Put|Delete)\(/g) ?? []).length, 1, 'Only the internal query is mapped outside the two groups.');
  const proxy = JSON.parse(read('services/api-gateway/appsettings.json')).ReverseProxy;
  for (const [name, route] of Object.entries(proxy.Routes))
    assert.doesNotMatch(route.Match?.Path ?? '', /^\/internal/i, `${name} must not expose an internal path.`);
  assert.match(read('services/billing-service/Billing.API/Program.cs'), /InternalServiceAuthentication\.ValidateConfiguration\(builder\.Configuration\)/);
  for (const file of ['docker-compose.billing.yml', 'docker-compose.billing.production.yml'])
    assert.match(read(file), /INTERNAL_SERVICE_API_KEY: \$\{INTERNAL_SERVICE_API_KEY:\?/, file);
});

test('an approval is single-shot and a payment is never deleted', () => {
  const orders = read('services/billing-service/Billing.Infrastructure/BillingOrders.cs');
  assert.match(orders, /FROM billing\.orders WHERE \\"Id\\" = \{id\} FOR UPDATE/);
  assert.doesNotMatch(orders, /db\.Payments\.Remove|Payments[\s\S]{0,80}ExecuteDeleteAsync/);
  const context = read('services/billing-service/Billing.Infrastructure/BillingDbContext.cs');
  assert.match(context, /HasIndex\(item => item\.OrderId\)\.IsUnique\(\);/, 'An order is paid once.');
  assert.match(context, /HasDatabaseName\("IX_orders_one_pending_per_plan"\)/);
});

test('only an exact match is approved without a person, and a statement line is stored and used once', () => {
  const statements = read('services/billing-service/Billing.Infrastructure/BillingBankStatements.cs');
  // Code known, order still waiting, amount equal: each failing check leaves the line for the admin with a reason.
  assert.match(statements, /if \(line\.PaymentCode is null\)[\s\S]*?return;[\s\S]*?if \(order is null\)[\s\S]*?return;[\s\S]*?order\.Status != OrderStatus\.Pending \|\| order\.ExpiresAt <= now[\s\S]*?return;[\s\S]*?order\.Amount != line\.Amount[\s\S]*?return;[\s\S]*?line\.Status = BankTransactionStatus\.Matched;\s*if \(!await IsAutomaticAsync\(order, cancellationToken\)\) return;/);
  assert.match(statements, /modes\.All\(mode => mode == BankTransferApproval\.Automatic\)/, 'A bundle follows its strictest product.');
  assert.match(statements, /StatementRules\.StatementIban\(rows, mapping\.HeaderRow\) is \{ \} iban && iban != account\.Iban/);
  const orders = read('services/billing-service/Billing.Infrastructure/BillingOrders.cs');
  assert.match(orders, /line\.Status == BankTransactionStatus\.Approved \|\| line\.PaymentId is not null[\s\S]*?Billing\.BankTransactionUsed/);
  assert.match(orders, /line\.Amount != order\.Amount[\s\S]*?Billing\.AmountMismatch/);
  const context = read('services/billing-service/Billing.Infrastructure/BillingDbContext.cs');
  assert.match(context, /HasIndex\(item => item\.Fingerprint\)\.IsUnique\(\);/);
  const endpoints = read('services/billing-service/Billing.API/BankStatementEndpoints.cs');
  assert.match(endpoints, /MapGroup\("\/api\/billing\/v1\/admin"\)\.RequireAuthorization\(BillingPolicies\.Admin\)/);
  assert.doesNotMatch(endpoints, /\bapp\.Map(Get|Post|Put|Delete)\(|AllowAnonymous/, 'Statements are the admin\'s alone.');
  // The real statement the layout was learned from holds names, IBANs and amounts: it never enters the repository.
  assert.doesNotMatch(read('tests/Integration/Billing.IntegrationTests/BankStatementTests.cs'), /HESAPEKSTRE_\d/);
});

test('seats are given by product services inside the network, and buying for an institution asks Identity', () => {
  const endpoints = read('services/billing-service/Billing.API/SeatEndpoints.cs');
  assert.match(endpoints, /MapGroup\("\/internal\/billing\/v1\/seats"\)\.AllowAnonymous\(\)\.AddEndpointFilter\([\s\S]*?InternalServiceAuthentication\.IsValid\([\s\S]*?Results\.Unauthorized\(\)/);
  assert.match(endpoints, /service\.MapPost\("\/assign"/);
  assert.match(endpoints, /service\.MapPost\("\/release"/);
  // No signed-in user gives a seat directly: only the product knows who may sit in a pool.
  assert.doesNotMatch(endpoints, /user\.Map(Post|Put|Delete)\("\/subscriptions\/\{id:guid\}\/seats/);
  assert.doesNotMatch(endpoints, /\bapp\.Map(Get|Post|Put|Delete)\(/);
  const orders = read('services/billing-service/Billing.Infrastructure/BillingOrders.cs');
  assert.match(orders, /institutions is null \? \[\] : await institutions\.GetAdministeredAsync\(user\.Id, cancellationToken\)/);
  assert.match(orders, /Billing\.OrganizationNotAdministered/);
  const seats = read('services/billing-service/Billing.Infrastructure/BillingSeats.cs');
  assert.match(seats, /FROM billing\.subscriptions WHERE \\"Id\\" = \{candidate\.Id\} FOR UPDATE/, 'Counting and taking a seat is one locked step.');
  const identity = read('services/identity-service/Identity.API/Controllers/InternalBillingController.cs');
  assert.match(identity, /\[Route\("api\/internal\/billing"\)\]/);
  assert.match(identity, /\[HttpGet\("administered-institutions"\)\]\s*\[AllowAnonymous\]\s*\[InternalServiceKey\]/);
  const client = read('services/billing-service/Billing.API/IdentityBillingInstitutions.cs');
  assert.match(client, /Billing\.IdentityUnavailable/, 'While Identity cannot be reached nobody is taken for an admin.');
  for (const file of ['docker-compose.billing.yml', 'docker-compose.billing.production.yml'])
    assert.match(read(file), /Services__IdentityService: http:\/\/identity-service:8080/, file);
});

test('coupons are counted from live orders, withdrawal follows the legal time, and invoices are the buyer\'s and the admin\'s', () => {
  const orders = read('services/billing-service/Billing.Infrastructure/BillingOrders.cs');
  assert.match(orders, /FROM billing\.coupons WHERE \\"Code\\" = \{normalized\} FOR UPDATE/, 'Counting and taking a coupon is one locked step.');
  assert.match(orders, /item\.CouponCode == normalized && \(item\.Status == OrderStatus\.Pending \|\| item\.Status == OrderStatus\.Paid\)/);
  assert.match(orders, /item\.OrderId == order\.Id && item\.Status == WithdrawalStatus\.Requested/, 'A reversal settles a waiting withdrawal.');
  const rules = read('services/billing-service/Billing.Domain/Extras.cs');
  assert.match(rules, /public const int WithdrawalDays = 14;/);
  assert.match(rules, /public const int MaxInvoiceBytes = 10 \* 1024 \* 1024;/);
  const extras = read('services/billing-service/Billing.Infrastructure/BillingExtras.cs');
  assert.match(extras, /item\.Id == orderId && item\.OrderedByUserId == userId/, 'Only the buyer asks to withdraw.');
  assert.match(extras, /item\.OrderedByUserId == owner \|\| item\.BeneficiaryUserId == owner/, 'Only the buyer opens the invoice.');
  assert.match(extras, /Billing\.TrialUsed/);
  const endpoints = read('services/billing-service/Billing.API/ExtrasEndpoints.cs');
  for (const path of ['/coupons', '/withdrawals', '/withdrawals/{id:guid}/review', '/orders/{id:guid}/invoice'])
    assert.ok(endpoints.includes(`admin.Map`) && endpoints.includes(`"${path}"`), `${path} is an admin endpoint`);
  assert.doesNotMatch(endpoints, /\bapp\.Map(Get|Post|Put|Delete)\(|AllowAnonymous/);
  const context = read('services/billing-service/Billing.Infrastructure/BillingDbContext.cs');
  assert.match(context, /HasDatabaseName\("IX_withdrawals_one_live_per_order"\)/);
});

test('billing notices reach the notification service on their own queue, and retention has its migration', () => {
  const endpoints = read('services/notification-service/Notification.API/Messaging/NotificationReceiveEndpoints.cs');
  assert.match(endpoints, /new\("billing-notice", typeof\(BillingNoticeConsumer\)\)/);
  const events = read('shared/EduPlatform.Shared.Contracts/Events/Billing/BillingEvents.cs');
  assert.match(events, /record BillingNoticeV1\(/);
  const consumer = read('services/notification-service/Notification.Application/Consumers/BillingNoticeConsumer.cs');
  for (const kind of [...events.matchAll(/public const string (\w+) = "/g)].map((m) => m[1]))
    assert.match(consumer, new RegExp(`BillingNoticeKinds\\.${kind}\\b`), `${kind} needs a text.`);
  const program = read('services/billing-service/Billing.API/Program.cs');
  assert.match(program, /IBillingLifecycle/);
  assert.match(read('services/billing-service/Billing.Domain/Extras.cs'), /AddYears\(10\)|RetentionYears = 10/);
});

test('Aboneliğim is one shared page, wrapped by each client application, and the admin has an overview', () => {
  const shared = read('clients/shared/billing/my-subscription.ts');
  assert.doesNotMatch(shared, /from '@angular|from 'rxjs/, 'The shared page stays framework-free: each application compiles it with its own packages.');
  for (const app of ['admin-panel', 'speed-reading']) {
    const wrapper = read(`clients/${app}/src/app/shared/billing/my-subscription.component.ts`);
    assert.match(wrapper, /template: MY_SUBSCRIPTION_TEMPLATE/, app);
    assert.match(wrapper, /new MySubscriptionController\(/, app);
  }
  const endpoints = read('services/billing-service/Billing.API/AdministrationEndpoints.cs');
  assert.match(endpoints, /MapGroup\("\/api\/billing\/v1\/admin"\)\.RequireAuthorization\(BillingPolicies\.Admin\)/);
  for (const route of ['/overview', '/subscriptions"', '/subscriptions/{id:guid}/extend', '/subscriptions/{id:guid}/cancel'])
    assert.ok(endpoints.includes(route), route);
});

test('Hızlı Okuma asks billing who may train only where billing runs', () => {
  const client = read('services/speed-reading-service/SpeedReading.Infrastructure/ExternalServices/BillingAccessClient.cs');
  assert.match(client, /GetValue\("Billing:Enabled", false\)/, 'Without the overlay the product asks for no subscription.');
  assert.match(client, /\/internal\/billing\/v1\/entitlements\?userId=/);
  assert.match(client, /InternalServiceAuthentication\.HeaderName/);
  const middleware = read('services/speed-reading-service/SpeedReading.API/Security/SpeedReadingSubscriptionAccessMiddleware.cs');
  assert.match(middleware, /ISpeedReadingAccess accessDecisions/);
  assert.doesNotMatch(middleware, /ISpeedReadingSubscription/);
  for (const file of ['docker-compose.billing.yml', 'docker-compose.billing.production.yml']) {
    const overlay = read(file);
    assert.match(overlay, /speed-reading-service:\s*\n\s+environment:[\s\S]*?Billing__Enabled[:=]\s*"?true/, file);
    assert.match(overlay, /Services__BillingService[:=]\s*http:\/\/billing-service:8080/, file);
  }
  const program = read('services/speed-reading-service/SpeedReading.API/Program.cs');
  assert.match(program, /AddConsumer<SpeedReading\.Application\.Subscription\.SpeedReadingBillingEntitlementsChangedConsumer>/);
  assert.match(program, /AddConsumer<SpeedReading\.Application\.Subscription\.SpeedReadingBillingSettingsChangedConsumer>/);
});

test('an institution gives the seats of its Hızlı Okuma plan through the product, which checks who may sit', () => {
  const controller = read('services/speed-reading-service/SpeedReading.API/Controllers/SpeedReadingInstitutionSeatsController.cs');
  assert.match(controller, /\[Route\("api\/speed-reading\/institutions\/\{institutionId:guid\}\/seats"\)\]/);
  assert.match(controller, /\[Authorize\(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner"\)\]/);
  assert.match(controller, /role: SpeedReadingInstitutionMemberRole\.Student, isActive: true, memberUserId: studentId/);
  const client = read('services/speed-reading-service/SpeedReading.Infrastructure/ExternalServices/BillingSeatsClient.cs');
  assert.match(client, /\/internal\/billing\/v1\/seats/);
  const wrapper = read('clients/admin-panel/projects/staff-portal/src/app/shared/billing/my-subscription.component.ts');
  assert.match(wrapper, /template: MY_SUBSCRIPTION_TEMPLATE/);
});

test('Koçluk asks billing which students may use it only where billing runs', () => {
  const client = read('services/coaching-service/Coaching.Infrastructure/ExternalServices/CoachingBillingAccessClient.cs');
  assert.match(client, /GetValue\("Billing:Enabled", false\)/);
  assert.match(client, /\/internal\/billing\/v1\/entitlements\?userId=/);
  assert.match(client, /BillingProductCodes\.Coaching/);
  const service = read('services/coaching-service/Coaching.Infrastructure/Management/CoachingSubscriptionService.cs');
  assert.match(service, /CoachingBillingRules\.Summary\(billing is \{ Enabled: true \} \? await billing\.GetAsync\(userId, cancellationToken\) : Open\)/);
  assert.doesNotMatch(service, /CoachingDbContext|CoachingSubscriptions\b/, 'Koçluk keeps no subscription records of its own any more.');
  for (const file of ['docker-compose.billing.yml', 'docker-compose.billing.production.yml'])
    assert.match(read(file), /coaching-service:\s*\n\s+environment:[\s\S]*?Billing__Enabled[:=]\s*"?true/, file);
});

test('Koçluk gives teacher and institution seats at billing, and frees them when a student leaves', () => {
  const client = read('services/coaching-service/Coaching.Infrastructure/ExternalServices/CoachingBillingSeatsClient.cs');
  assert.match(client, /\/internal\/billing\/v1\/seats/);
  assert.match(client, /ownerUserId=/);
  assert.match(client, /organizationId=/);
  const service = read('services/coaching-service/Coaching.Infrastructure/Management/CoachingSubscriptionService.cs');
  assert.match(service, /IsAssignedIndependentTeacherStudentAsync\(teacherId, studentId, cancellationToken\)\) return false;\s*\n\s*var given = \(await billingSeats\.AssignAsync/,
    'A teacher seat goes only to a student assigned to that teacher.');
  const store = read('services/coaching-service/Coaching.Infrastructure/Profiles/CoachingProfileStore.cs');
  assert.equal((store.match(/billingSeats\.ReleaseWhenLeavingAsync\(/g) ?? []).length, 3);
});

test('a Hızlı Okuma student whose institution membership ends does not keep the seat', () => {
  const members = read('services/speed-reading-service/SpeedReading.API/Controllers/SpeedReadingInstitutionMembersController.cs');
  assert.match(members, /!request\.IsActive && request\.Role == SpeedReadingInstitutionMemberRole\.Student\s*\n\s*&& billingSeats is not null\)\s*\{\s*await billingSeats\.ReleaseWhenLeavingAsync\(institutionId, userId, actorId, cancellationToken\);/);
});

test('Koçluk shows the shared Aboneliğim page; a subscription is bought in the student\'s own account', () => {
  const student = read('clients/admin-panel/src/app/features/coaching-portal/pages/student-payments.component.ts');
  assert.match(student, /<app-my-subscription product="coaching" \/>/);
  const parent = read('clients/admin-panel/src/app/features/coaching-portal/parent/parent-payments.component.ts');
  assert.doesNotMatch(parent, /<form|sendTransfer|edu-coaching-checkout/, 'The parent sees the child\'s subscription; there is no parent order (decision 15.11.2026).');
  assert.doesNotMatch(read('clients/admin-panel/src/app/features/coaching-portal/parent/coaching-parent.service.ts'), /bank-transfer-requests/);
  for (const page of ['coaching-teacher-subscription', 'coaching-institution-subscription'])
    assert.match(read(`clients/admin-panel/projects/staff-portal/src/app/features/coaching/${page}.component.ts`), /<staff-my-subscription product="coaching"/, page);
  assert.doesNotMatch(read('services/billing-service/Billing.Application/OrderContracts.cs'), /BeneficiaryUserId = null/, 'Orders are for the buyer or the buyer\'s institution.');
});

test('the products keep no subscription, payment or sales code of their own', () => {
  for (const gone of ['services/speed-reading-service/SpeedReading.Infrastructure/Legacy/LegacySpeedReadingSubscription.cs',
    'services/speed-reading-service/SpeedReading.Infrastructure/Payments/IyzicoPaymentProvider.cs',
    'services/speed-reading-service/SpeedReading.API/Controllers/BankTransferPaymentsController.cs',
    'services/coaching-service/Coaching.Infrastructure/Management/CoachingSalesService.cs',
    'services/coaching-service/Coaching.Infrastructure/Management/CoachingSubscriptionLifecycle.cs',
    'services/coaching-service/Coaching.API/Controllers/CoachingSubscriptionsAdminController.cs',
    'clients/admin-panel/shared/coaching-checkout.component.ts',
    'clients/speed-reading/src/app/features/public/payment/payment.component.ts'])
    assert.throws(() => read(gone), /ENOENT/, gone);
  const coaching = read('services/coaching-service/Coaching.Application/Subscriptions/CoachingSubscriptionContracts.cs');
  assert.doesNotMatch(coaching, /BankTransfer|Coupon|Invoice|Withdrawal|CreateSubscriptionAsync/);
  const speedReading = read('services/speed-reading-service/SpeedReading.API/Controllers/SubscriptionsController.cs');
  assert.doesNotMatch(speedReading, /HttpPost|HttpPut|HttpDelete/, 'Hızlı Okuma only answers who has access.');
});

test('billing takes part in an account erasure, and Identity waits for it only where billing runs', () => {
  const program = read('services/billing-service/Billing.API/Program.cs');
  assert.match(program, /AddConsumer<BillingErasureAssessmentRequestedConsumer>/);
  assert.match(program, /AddConsumer<BillingErasureExecutionRequestedConsumer>/);
  const policy = read('services/identity-service/Identity.Application/DataSubjectRequests/DataSubjectRequestUseCases.cs');
  assert.match(policy, /billingEnabled\s*\n?\s*\? \["Coaching", "Notification", "SpeedReading", "CourseManagement", "Billing"\]/);
  assert.match(read('services/identity-service/Identity.API/Program.cs'), /BillingEnabled = builder\.Configuration\.GetValue\("Billing:Enabled", false\)/);
  for (const file of ['docker-compose.billing.yml', 'docker-compose.billing.production.yml'])
    assert.match(read(file), /identity-service:\s*\n\s+environment:\s*\n\s+-? ?Billing__Enabled[:=]\s*"?true/, file);
});

test('Kurs Yönetimi reads a workspace plan and its student limit from billing where billing runs', () => {
  const api = 'services/course-management-service/CourseManagement.API/';
  const plans = read(api + 'WorkspaceBillingPlans.cs');
  assert.match(plans, /internal\/billing\/v1\/entitlements\?userId=\{ownerUserId:D\}&product=\{BillingProductCodes\.CourseManagement\}/);
  assert.match(plans, /WorkspaceEntitlement = "course-management\.workspace"/);
  assert.match(read(api + 'Program.cs'), /AddHttpClient<WorkspaceBillingPlans>/);
  assert.match(read(api + 'WorkspaceSubscriptionWriteAccessFilter.cs'), /if \(billing\.Enabled\)[\s\S]*?Subscription\.ReadOnly[\s\S]*?db\.WorkspaceSubscriptions/);
  assert.match(read(api + 'WorkspaceStudentCapacity.cs'), /if \(billing\.Enabled\)[\s\S]*?plan\.CanHold\(active \+ additionalStudents\)/);
  assert.match(read(api + 'CourseManagementAdminEndpoints.cs'), /Subscription\.ManagedByBilling/);
  for (const file of ['docker-compose.course-management.yml', 'docker-compose.course-management.production.yml'])
    assert.match(read(file), /Billing__Enabled: \$\{BILLING_ENABLED:-false\}\s*\n\s+Services__BillingService: http:\/\/billing-service:8080/, file);
});

test('an institution licence is no longer set or shown through Identity', () => {
  const identity = 'services/identity-service/';
  for (const file of ['Identity.Application/Commands/ManageInstitutionCommands.cs', 'Identity.Application/DTOs/InstitutionDtos.cs',
    'Identity.API/Controllers/InstitutionsController.cs'])
    assert.doesNotMatch(read(identity + file), /LicenseType|MaxStudents|MaxTeachers|SubscriptionEndDate/, file);
  assert.doesNotMatch(read(identity + 'Identity.Domain/Entities/Institution.cs'), /UpgradeLicense|CanAddStudent|IsSubscriptionActive/);
  assert.doesNotMatch(read('clients/admin-panel/src/app/features/identity/pages/institution-list.ts'), /licenseType|maxStudents|maxTeachers/);
  const settings = 'clients/admin-panel/projects/staff-portal/src/app/features/speed-reading/speed-reading-institution-settings';
  assert.doesNotMatch(read(settings + '.service.ts') + read(settings + '.component.html'), /institution-access|maxStudents|subscriptionEndDate/);
});
