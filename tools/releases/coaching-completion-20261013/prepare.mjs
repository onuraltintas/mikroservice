import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync, copyFileSync, existsSync, unlinkSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const snapshot = resolve(root, '.tmp/coaching-release-ac014834');
assert.ok(existsSync(resolve(snapshot, 'services/coaching-service/Coaching.API/Program.cs')));
const restore = (revision, path) => writeFileSync(resolve(snapshot, path), execFileSync('git', ['show', `${revision}:${path}`], { cwd: root }));
const replaceOnce = (text, before, after) => {
  assert.equal(text.split(before).length, 2, `Expected one source anchor: ${before.slice(0, 70)}`);
  return text.replace(before, after);
};

// These source files were verified against the PDB of today's live Google login release.
const identityBase = 'tools/releases/google-direct-20261013/base/services/identity-service/Identity.Application/';
for (const path of ['Authorization/StaffProductAccessPolicy.cs', 'Commands/Login/LoginCommandHandler.cs',
  'Commands/RefreshToken/RefreshTokenCommand.cs', 'Commands/RefreshToken/RefreshTokenCommandHandler.cs',
  'Commands/GoogleLogin/GoogleLoginCommandHandler.cs', 'LegalPages/RegistrationLegalConsentPolicy.cs']) {
  copyFileSync(resolve(root, identityBase, path), resolve(snapshot, 'services/identity-service/Identity.Application', path));
}
// The future course service is not part of this deployment; its handler is uncommitted.
const courseController = resolve(snapshot, 'services/identity-service/Identity.API/Controllers/CourseManagementAuthController.cs');
if (existsSync(courseController)) unlinkSync(courseController);

const staffRoot = 'clients/admin-panel/projects/staff-portal/src/app/';
for (const path of ['app.component.ts', 'app.component.html', 'auth/staff-auth-recaptcha.interceptor.ts',
  'auth/staff-auth.interceptor.ts', 'auth/staff-auth.models.ts', 'auth/staff-auth.service.ts']) restore('0bffbe36', staffRoot + path);
let staff = readFileSync(resolve(snapshot, staffRoot, 'app.component.ts'), 'utf8').replaceAll('\r\n', '\n');
staff = replaceOnce(staff, "import { Component, inject }", "import { Component, ViewChild, inject }");
staff = replaceOnce(staff, 'export class AppComponent {', `export class AppComponent {
  @ViewChild(CoachingTeacherWorkspaceComponent) coachingTeacherWorkspace?: CoachingTeacherWorkspaceComponent;
  @ViewChild(CoachingInstitutionWorkspaceComponent) coachingInstitutionWorkspace?: Pick<CoachingInstitutionWorkspaceComponent, 'canLeavePage'>;
  private canLeaveCoachingWorkspace(): boolean {
    return (this.coachingTeacherWorkspace?.canLeavePage() ?? true) && (this.coachingInstitutionWorkspace?.canLeavePage() ?? true);
  }`);
staff = replaceOnce(staff, "    if (role === 'institution' && !this.isInstitutionManager) return;",
  "    if (role === 'institution' && !this.isInstitutionManager) return;\n    if (role !== this.activeWorkspaceRole && !this.canLeaveCoachingWorkspace()) return;");
staff = replaceOnce(staff, '    if (product === this.auth.activeProduct()) return;',
  '    if (product === this.auth.activeProduct()) return;\n    if (!this.canLeaveCoachingWorkspace()) return;');
staff = replaceOnce(staff, '  async logout(): Promise<void> {',
  '  async logout(): Promise<void> {\n    if (!this.canLeaveCoachingWorkspace()) return;');
writeFileSync(resolve(snapshot, staffRoot, 'app.component.ts'), staff);

// Retain the live admin navigation; apply only the reviewed finance additions.
const adminNavigation = ['clients/admin-panel/src/app/features/dashboard/dashboard.routes.ts',
  'clients/admin-panel/src/app/features/dashboard/layout/sidebar/sidebar.ts'];
for (const path of adminNavigation) restore('6f315ff3', path);
const navigationPatch = execFileSync('git', ['diff', 'ac014834^', 'ac014834', '--', ...adminNavigation], { cwd: root });
execFileSync('git', ['apply', '--directory=.tmp/coaching-release-ac014834', '-'], { cwd: root, input: navigationPatch });
for (const path of [staffRoot + 'app.component.ts', ...adminNavigation]) {
  assert.ok(!readFileSync(resolve(snapshot, path), 'utf8').includes('course-management'), `Unreleased course navigation: ${path}`);
}
console.log('Isolated coaching snapshot prepared; live login and non-coaching navigation retained.');
