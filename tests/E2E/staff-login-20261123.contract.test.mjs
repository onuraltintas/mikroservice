import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// User decision 23.11.2026: the staff portal no longer asks which product to open. The identity service opens the
// product where the account has a role for the address (teacher or institution); with two, the last used one, otherwise
// maxkoç. The other product stays one click away in the workspace menu; maxkampüs (pilot page) keeps its own sign-in.
const root = new URL('../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, root), 'utf8');

test('identity signs staff in without a product and says which product it opened', () => {
  const controller = read('services/identity-service/Identity.API/Controllers/AuthController.cs');
  assert.match(controller, /\[HttpPost\("staff\/login"\)\]/);
  assert.match(controller, /Staff: new StaffSignIn\(workspace, preferred\)/);
  const handler = read('services/identity-service/Identity.Application/Commands/Login/LoginCommandHandler.cs');
  assert.match(handler, /StaffProductAccessPolicy\.ResolveSignInProduct\(user, staff\.Workspace, staff\.PreferredProduct\)/);
  const policy = read('services/identity-service/Identity.Application/Authorization/StaffProductAccessPolicy.cs');
  assert.match(policy, /PlatformProduct\.Coaching or PlatformProduct\.SpeedReading/, 'maxkampüs keeps its own sign-in');
  assert.match(read('services/identity-service/Identity.API/Security/AuthRecaptchaSecurity.cs'), /segments\[2\], "staff"/, 'the sign-in keeps reCAPTCHA');
  assert.match(read('services/api-gateway/appsettings.json'), /"\/api\/auth\/staff\/login"/, 'the sign-in keeps the login rate limit');
});

test('the staff portal sign-in shows no product choice outside the maxkampüs pilot page', () => {
  const page = read('clients/admin-panel/projects/staff-portal/src/app/app.component.html');
  assert.match(page, /@if \(courseManagementOffered\) \{\s*<div class="staff-login__products"/);
  assert.match(page, /Hesabınızın yetkili olduğu ürün açılır\./);
  const service = read('clients/admin-panel/projects/staff-portal/src/app/auth/staff-auth.service.ts');
  assert.match(service, /\/auth\/staff\/login`/);
  assert.match(service, /preferredProduct: preferredProduct \?\? this\.getLastProduct\(\)/);
  assert.match(read('clients/admin-panel/projects/staff-portal/src/app/app.component.ts'), /this\.siteRole,/, 'the address tells which role to open');
});
