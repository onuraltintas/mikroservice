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
    assert.doesNotMatch(read(file), /billing/i, `${file} must not mention Billing before its release.`);
  assert.doesNotMatch(read('.github/workflows/production-images.yml'), /billing/i);
  assert.doesNotMatch(read('infrastructure/scripts/pull-release-images.sh'), /billing/i);

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
