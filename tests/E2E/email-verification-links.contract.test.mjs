import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// PR #111 second review H1: a verification e-mail must open a page that exists and reads the link's
// query names. Coaching (admin-panel, PublicApp:BaseUrl) verifies at /auth/confirm-email; the
// speed-reading app at /auth/verify-email. Unknown auth routes in the admin-panel redirect away.
const read = path => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');
const options = read('services/notification-service/Notification.Application/Configuration/PublicAppUrlOptions.cs');
const constant = name => {
  const match = new RegExp(`public const string ${name} = "([^"]+)";`).exec(options);
  assert.ok(match, `${name} is declared in PublicAppUrlOptions`);
  return match[1];
};
const queryNames = template => [...template.matchAll(/[?&]([A-Za-z]+)=/g)].map(m => m[1]).sort();
const linkTemplate = pathConstant => {
  const match = new RegExp(`\\{${pathConstant}\\}(\\?[^"]+)"`).exec(options);
  assert.ok(match, `a link is built from ${pathConstant}`);
  return match[1];
};

test('coaching verification links open the admin-panel confirm-email page with the names it reads', () => {
  const path = constant('CoachingEmailVerificationPath');
  assert.equal(path, '/auth/confirm-email');
  const routes = read('clients/admin-panel/src/app/features/auth/auth.routes.ts');
  assert.match(routes, new RegExp(`path: '${path.replace('/auth/', '')}'[\\s\\S]{0,200}confirm-email\\.component`));
  const component = read('clients/admin-panel/src/app/features/auth/confirm-email/confirm-email.component.ts');
  const read_ = [...component.matchAll(/queryParamMap\.get\('([^']+)'\)/g)].map(m => m[1]).sort();
  assert.deepEqual(read_, ['token', 'userId']);
  assert.deepEqual(queryNames(linkTemplate('CoachingEmailVerificationPath')), read_);
});

test('speed-reading verification links open the verify-email page with the names it reads', () => {
  const path = constant('SpeedReadingEmailVerificationPath');
  assert.equal(path, '/auth/verify-email');
  const routes = read('clients/speed-reading/src/app/features/auth/auth.routes.ts');
  assert.match(routes, new RegExp(`path: '${path.replace('/auth/', '')}'[\\s\\S]{0,200}verify-email\\.component`));
  const component = read('clients/speed-reading/src/app/features/auth/verify-email/verify-email.component.ts');
  const read_ = [...component.matchAll(/params\['([^']+)'\]/g)].map(m => m[1]).sort();
  assert.deepEqual(read_, ['token', 'userId']);
  assert.deepEqual(queryNames(linkTemplate('SpeedReadingEmailVerificationPath')), read_);
});

// User decision 05.11.2026: Coaching e-mail links (verification, parent summary and link, subscription
// notices, invitations) go to the Coaching site onuraltintas.net; management links stay on eduivme.com.
test('production sends Coaching links to onuraltintas.net and management links to eduivme.com', () => {
  const compose = read('docker-compose.production.yml');
  const defaults = name => [...compose.matchAll(new RegExp(`\\$\\{${name}:-([^}]+)\\}`, 'g'))].map(m => m[1]);
  assert.ok(defaults('PUBLIC_APP_BASE_URL').length >= 2, 'coaching and notification services read PUBLIC_APP_BASE_URL');
  assert.deepEqual([...new Set(defaults('PUBLIC_APP_BASE_URL'))], ['https://onuraltintas.net']);
  assert.deepEqual([...new Set(defaults('PUBLIC_ADMIN_PANEL_URL'))], ['https://eduivme.com']);
  assert.deepEqual([...new Set(defaults('PUBLIC_STAFF_PORTAL_URL'))], ['https://onuraltintas.net/staff']);
  assert.match(compose, /Coaching__PublicBaseUrl: \$\{PUBLIC_APP_BASE_URL:-/);
  assert.match(compose, /PublicApp__BaseUrl: \$\{PUBLIC_APP_BASE_URL:-/);
  assert.match(compose, /PublicApp__AdminPanelUrl: \$\{PUBLIC_ADMIN_PANEL_URL:-/);
  // Staging keeps its own address.
  assert.match(read('docs/VPS_STAGING_DEPLOYMENT.md'), /PUBLIC_APP_BASE_URL=https:\/\/staging\.onuraltintas\.net/);
});

test('onuraltintas.net serves the pages the Coaching links open', () => {
  const caddy = read('infrastructure/caddy/Caddyfile.production.litespeed');
  const site = /onuraltintas\.net:80, www\.onuraltintas\.net:80 \{([\s\S]*?)\n\}/.exec(caddy);
  assert.ok(site, 'the Coaching site block exists');
  assert.match(site[1], /reverse_proxy admin-panel:4000/);
  const blocked = [...site[1].matchAll(/@management path ([^\n]+)/g)].map(m => m[1]).join(' ');
  for (const path of ['/auth/confirm-email', '/auth/login', '/auth/reset-password', '/coaching-portal'])
    assert.ok(!blocked.split(/\s+/).some(prefix => path === prefix || path.startsWith(prefix.replace('/*', '/'))), `${path} is not blocked`);
  const hosts = read('clients/admin-panel/src/server-config.ts');
  assert.match(hosts, /'onuraltintas\.net'/);
});

// Final review: password reset links open the reset page of the product's own site with the names it reads.
test('password reset links open reset-password pages that read token and email', () => {
  const path = constant('PasswordResetPath');
  assert.equal(path, '/auth/reset-password');
  assert.deepEqual(queryNames(linkTemplate('PasswordResetPath')), ['email', 'token']);
  const pages = [
    ['clients/admin-panel/src/app/features/auth/auth.routes.ts', 'clients/admin-panel/src/app/features/auth/reset-password/reset-password.component.ts'],
    ['clients/speed-reading/src/app/features/auth/auth.routes.ts', 'clients/speed-reading/src/app/features/auth/reset-password/reset-password.component.ts'],
  ];
  for (const [routesFile, componentFile] of pages) {
    assert.match(read(routesFile), /path: 'reset-password'[\s\S]{0,200}reset-password\.component/);
    const names = [...read(componentFile).matchAll(/params\['([^']+)'\]/g)].map(m => m[1]).sort();
    assert.deepEqual([...new Set(names)], ['email', 'token']);
  }
});

test('production sends speed-reading links to masterhizliokuma.com', () => {
  const compose = read('docker-compose.production.yml');
  const defaults = [...compose.matchAll(/\$\{SPEED_READING_PUBLIC_BASE_URL:-([^}]+)\}/g)].map(m => m[1]);
  assert.ok(defaults.length >= 2, 'speed-reading service and notification read SPEED_READING_PUBLIC_BASE_URL');
  assert.deepEqual([...new Set(defaults)], ['https://masterhizliokuma.com']);
  assert.match(compose, /PublicApp__SpeedReadingBaseUrl: \$\{SPEED_READING_PUBLIC_BASE_URL:-/);
  assert.match(options, /SpeedReadingBaseUrl[\s\S]*IsSpeedReadingProduct\(role, product\) && IsValidBaseUrl\(SpeedReadingBaseUrl\)/);
});
