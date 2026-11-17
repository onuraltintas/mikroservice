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

// User decision 05.11.2026 and alan adı planı 18.11.2026: Coaching e-mail links go to each group's own address
// (students maxkoc.com, parents veli.maxkoc.com, teachers ogretmen.eduivme.com, institutions kurum.eduivme.com);
// management links go to admin.eduivme.com.
test('production sends Coaching links to the group addresses and management links to admin.eduivme.com', () => {
  const compose = read('docker-compose.production.yml');
  const defaults = name => [...compose.matchAll(new RegExp(`\\$\\{${name}:-([^}]+)\\}`, 'g'))].map(m => m[1]);
  assert.ok(defaults('PUBLIC_APP_BASE_URL').length >= 2, 'coaching and notification services read PUBLIC_APP_BASE_URL');
  assert.deepEqual([...new Set(defaults('PUBLIC_APP_BASE_URL'))], ['https://maxkoc.com']);
  assert.deepEqual([...new Set(defaults('PUBLIC_PARENT_BASE_URL'))], ['https://veli.maxkoc.com']);
  assert.deepEqual([...new Set(defaults('PUBLIC_ADMIN_PANEL_URL'))], ['https://admin.eduivme.com']);
  assert.deepEqual([...new Set(defaults('PUBLIC_STAFF_PORTAL_URL'))], ['https://ogretmen.eduivme.com/staff']);
  assert.deepEqual([...new Set(defaults('PUBLIC_INSTITUTION_PORTAL_URL'))], ['https://kurum.eduivme.com/staff']);
  assert.match(compose, /PublicApp__ParentBaseUrl: \$\{PUBLIC_PARENT_BASE_URL:-/);
  assert.match(compose, /PublicApp__InstitutionPortalUrl: \$\{PUBLIC_INSTITUTION_PORTAL_URL:-/);
  assert.doesNotMatch(compose, /onuraltintas\.net/);
  assert.match(compose, /Coaching__PublicBaseUrl: \$\{PUBLIC_APP_BASE_URL:-/);
  assert.match(compose, /PublicApp__BaseUrl: \$\{PUBLIC_APP_BASE_URL:-/);
  assert.match(compose, /PublicApp__AdminPanelUrl: \$\{PUBLIC_ADMIN_PANEL_URL:-/);
  // Staging keeps its own address.
  assert.match(read('docs/VPS_STAGING_DEPLOYMENT.md'), /PUBLIC_APP_BASE_URL=https:\/\/staging\.onuraltintas\.net/);
});

/** The Caddy block of a host (alan adı planı 18.11.2026): the block whose address list names exactly that host. */
function siteBlock(caddy, host) {
  for (const match of caddy.matchAll(/(?:^|\n)([^\s{#][^\n{]*)\{([\s\S]*?)\n\}/g)) {
    const names = match[1].split(',').map(name => name.trim().replace(/:80$/, ''));
    if (names.includes(host)) return match[2];
  }
  return null;
}

/** The paths a block sends elsewhere or refuses (matchers other than the API routes and the root). */
function movedPaths(block) {
  return [...block.matchAll(/@(?!api|health|signalr|root|staffRoot)\w+ path ([^\n]+)/g)].flatMap(m => m[1].trim().split(/\s+/));
}

test('each group address serves the auth pages the Coaching links open', () => {
  const caddy = read('infrastructure/caddy/Caddyfile.production.litespeed');
  assert.doesNotMatch(caddy, /onuraltintas\.net/);
  const hosts = read('clients/admin-panel/src/server-config.ts');
  for (const host of ['maxkoc.com', 'veli.maxkoc.com', 'ogretmen.eduivme.com', 'kurum.eduivme.com', 'admin.eduivme.com']) {
    const block = siteBlock(caddy, host);
    assert.ok(block, `the ${host} block exists`);
    assert.match(block, /reverse_proxy admin-panel:4000/);
    const moved = movedPaths(block);
    for (const path of ['/auth/confirm-email', '/auth/login', '/auth/reset-password', '/auth/confirm-email-change'])
      assert.ok(!moved.some(prefix => path === prefix || path.startsWith(prefix.replace('/*', '/'))), `${path} stays on ${host}`);
    assert.match(hosts, new RegExp(`'${host.replace(/\./g, '\\.')}'`));
  }
  // Students open the Coaching portal on maxkoc.com, parents their panel on veli.maxkoc.com.
  assert.match(siteBlock(caddy, 'maxkoc.com'), /@parentPages path \/coaching-portal\/parent \/coaching-portal\/parent\/\*\n\s*redir @parentPages https:\/\/veli\.maxkoc\.com\{uri\}/);
  assert.match(siteBlock(caddy, 'veli.maxkoc.com'), /redir @root \/coaching-portal\/parent/);
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

// E5 (#22): the e-mail change link opens the admin-panel page (Coaching site; management: the admin panel) that reads
// exactly the names the link carries, and that page is not sent elsewhere on any group address.
test('e-mail change links open the confirm-email-change page with the names it reads', () => {
  const path = constant('EmailChangeConfirmationPath');
  assert.equal(path, '/auth/confirm-email-change');
  const routes = read('clients/admin-panel/src/app/features/auth/auth.routes.ts');
  assert.match(routes, /path: 'confirm-email-change'[\s\S]{0,200}confirm-email-change\.component/);
  const component = read('clients/admin-panel/src/app/features/auth/confirm-email-change/confirm-email-change.component.ts');
  const names = [...component.matchAll(/queryParamMap\.get\('([^']+)'\)/g)].map(m => m[1]).sort();
  assert.deepEqual(names, ['token', 'userId']);
  assert.deepEqual(queryNames(linkTemplate('EmailChangeConfirmationPath')), names);
  // The link always goes to an admin-panel origin: the speed-reading app has no such page.
  assert.match(options, /BuildEmailChangeLink[\s\S]{0,300}ResolveBaseUrl\(role, "coaching"\)/);
  const caddy = read('infrastructure/caddy/Caddyfile.production.litespeed');
  for (const host of ['maxkoc.com', 'veli.maxkoc.com', 'admin.eduivme.com']) {
    const moved = movedPaths(siteBlock(caddy, host));
    assert.ok(!moved.some(prefix => path.startsWith(prefix.replace('/*', '/')) || path === prefix), host);
  }
});
