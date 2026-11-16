import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const read = (path) => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');

// 16.11.2026: "Kalıcı Sil" removed the Identity row only. The user's records stayed behind in every other service,
// and a linked parent or student could not be removed at all (500). An account is erased through the erasure flow.
test('the administrator cannot remove an account row directly any more', () => {
  const handler = read('services/identity-service/Identity.Application/Commands/DeleteUser/DeleteUserCommandHandler.cs');
  assert.doesNotMatch(handler, /DeleteUserAsync/);
  assert.match(handler, /Identity\.PermanentDeleteRetired/);

  const service = read('clients/admin-panel/src/app/core/services/identity.service.ts');
  assert.doesNotMatch(service, /\/\$\{userId\}\?permanent=\$\{/);
  assert.match(service, /data-subject-requests|dataSubjectRequestsUrl\}\/admin\/erasure/);
});

test('the user list opens an erasure request with a reason and sends the administrator to the requests page', () => {
  const page = read('clients/admin-panel/src/app/features/identity/pages/user-list/user-list.ts');
  const html = read('clients/admin-panel/src/app/features/identity/pages/user-list/user-list.html');
  assert.match(page, /openErasureForUser\(user\.userId, trimmed\)/);
  assert.match(page, /privacy-requests/);
  assert.match(page, /canErase = this\.can\(ADMIN_PERMISSIONS\.privacyManage\)/);
  assert.match(html, /@if \(canErase\(\)\) \{ <button \(click\)="eraseUser\(user\)"/);
  assert.doesNotMatch(html, /permanentDeleteUser|Kalıcı Sil/);
});

test('an erasure opened by the administrator is refused for the administrator, a system administrator and an erased account', () => {
  const useCases = read('services/identity-service/Identity.Application/DataSubjectRequests/DataSubjectRequestUseCases.cs');
  const handler = useCases.slice(useCases.indexOf('class OpenErasureRequestForUserCommandHandler'));
  assert.match(handler, /command\.UserId == administratorId/);
  assert.match(handler, /subject\.IsSystemAdministrator/);
  assert.match(handler, /subject\.AlreadyErased/);
  assert.match(handler, /HasActiveAsync/);
  assert.match(handler, /PublishAssessmentRequestedAsync/);
});

// 16.11.2026: after an erasure the emptied row stayed in the user list like any other user.
test('an erased account is left out of the user list and shown only through its own filter', () => {
  const repository = read('services/identity-service/Identity.Infrastructure/Repositories/Repositories.cs');
  assert.equal(repository.match(/!user\.Email\.EndsWith\(User\.ErasedEmailDomain\)/g)?.length, 2, 'the list and the summary');
  assert.match(repository, /GetErasedAsync[\s\S]*?user\.Email\.EndsWith\(User\.ErasedEmailDomain\)/);
  assert.match(read('services/identity-service/Identity.Domain/Entities/User.cs'), /Email = \$"erased-\{Id:N\}\{ErasedEmailDomain\}"/);

  const handler = read('services/identity-service/Identity.Application/Queries/GetAllUsers/GetAllUsersQueryHandler.cs');
  assert.match(handler, /request\.Erased[\s\S]*?scope\.Value\.InstitutionId is null/);

  const page = read('clients/admin-panel/src/app/features/identity/pages/user-list/user-list.ts');
  const html = read('clients/admin-panel/src/app/features/identity/pages/user-list/user-list.html');
  assert.match(page, /value: 'erased', label: 'Silinmiş hesaplar'/);
  assert.match(html, /\[class\.hidden\]="showErased\(\)"/);
});
