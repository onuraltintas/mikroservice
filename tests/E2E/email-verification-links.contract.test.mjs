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
