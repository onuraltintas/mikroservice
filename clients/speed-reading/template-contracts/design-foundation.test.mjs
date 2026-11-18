import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import test from 'node:test';

// Redesign 18.11.2026: one token set for maxkoç, eduivme and Master Hızlı Okuma (.claude/skills/eduivme-design/SKILL.md).
const repo = new URL('../../../', import.meta.url);
const read = (file) => readFileSync(new URL(file, repo), 'utf8');

test('the design tokens file is the generator output, and the generator still passes its contrast checks', () => {
  const before = read('clients/shared/styles/_design-tokens.scss');
  execFileSync('python3', ['tools/brand/design_tokens.py'], { cwd: new URL('.', repo), stdio: 'pipe' });
  assert.equal(read('clients/shared/styles/_design-tokens.scss'), before, 'run python3 tools/brand/design_tokens.py and commit the result');
  for (const brand of ['maxkoc', 'eduivme', 'ho']) assert.match(before, new RegExp(`\\[data-brand='${brand}'\\]`));
});

test('every client loads the tokens and the shared components', () => {
  for (const file of ['clients/speed-reading/src/styles.scss', 'clients/admin-panel/src/styles.scss', 'clients/admin-panel/projects/staff-portal/src/styles.scss']) {
    const styles = read(file);
    assert.match(styles, /shared\/styles\/design-tokens'/, file);
    assert.match(styles, /shared\/styles\/design-components'/, file);
  }
  assert.match(read('clients/admin-panel/angular.json'), /@fontsource\/ibm-plex-sans\/400\.css/);
  assert.match(read('clients/speed-reading/angular.json'), /@fontsource\/lexend\/400\.css/);
});

test('redesigned pages use tokens, not fixed colors', () => {
  for (const file of [
    'clients/shared/styles/_design-components.scss',
    'clients/admin-panel/src/app/features/auth/login/login.component.html',
    'clients/admin-panel/src/app/features/auth/login/login.component.scss',
    'clients/speed-reading/src/app/features/auth/login/login.component.html',
    'clients/speed-reading/src/app/features/auth/login/login.component.scss',
    'clients/speed-reading/src/app/features/auth/registration-shell.component.html',
    'clients/speed-reading/src/app/features/auth/registration-shell.component.scss',
  ]) {
    const text = read(file).replace(/\/\/.*$/gm, '');
    assert.doesNotMatch(text, /#[0-9a-fA-F]{3,8}\b|rgba?\(\s*\d/, `${file} has a fixed color; use a --ds-* token`);
  }
});

test('sign-in pages carry a brand and keep their test hooks', () => {
  const admin = read('clients/admin-panel/src/app/features/auth/login/login.component.html');
  assert.match(admin, /class="ds-auth" \[attr\.data-brand\]="page\.brand"/);
  assert.match(admin, /data-testid="login-features"/);
  const shell = read('clients/speed-reading/src/app/features/auth/registration-shell.component.html');
  assert.match(shell, /class="ds-auth" data-brand="ho"/);
});
