import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const read = (path) => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');

const robots = read('clients/speed-reading/public/robots.txt');
const directives = robots
  .split(/\r?\n/)
  .map((line) => line.replace(/#.*/, '').trim())
  .filter(Boolean);

// Google robots.txt matching: the most specific (longest) matching rule wins;
// on a tie Allow wins. `$` anchors the end of the URL path.
function googleAllows(path) {
  let best = { length: -1, allow: true };
  for (const line of directives) {
    const match = line.match(/^(Allow|Disallow):\s*(.*)$/i);
    if (!match || !match[2]) continue;
    const pattern = match[2];
    const anchored = pattern.endsWith('$');
    const body = anchored ? pattern.slice(0, -1) : pattern;
    const regex = new RegExp(
      '^' + body.split('*').map((part) => part.replace(/[.+?^${}()|[\]\\]/g, '\\$&')).join('.*') + (anchored ? '$' : ''),
    );
    if (!regex.test(path)) continue;
    const allow = match[1].toLowerCase() === 'allow';
    if (pattern.length > best.length || (pattern.length === best.length && allow)) {
      best = { length: pattern.length, allow };
    }
  }
  return best.allow;
}

test('the cookie page reads its text from the exact shared legal-pages endpoint', () => {
  const service = read('clients/speed-reading/src/app/core/services/platform-legal-pages.service.ts');
  const component = read('clients/speed-reading/src/app/features/legal/cookies/cookies.component.ts');
  const environment = read('clients/speed-reading/src/environments/environment.prod.ts');

  assert.match(environment, /apiUrl:\s*'\/api'/);
  assert.match(service, /\$\{environment\.apiUrl\}\/platform\/legal-pages/);
  assert.match(component, /getPage\('cookies'\)/);
});

test('robots.txt lets Googlebot fetch only the public cookie policy API path', () => {
  assert.ok(directives.includes('Disallow: /api/'), 'the generic API block must stay in place');
  assert.ok(directives.includes('Allow: /api/platform/legal-pages/cookies$'));

  assert.equal(googleAllows('/api/platform/legal-pages/cookies'), true);
  assert.equal(googleAllows('/legal/cookies'), true);

  for (const blocked of [
    '/api/platform/legal-pages',
    '/api/platform/legal-pages/privacy',
    '/api/platform/legal-pages/kvkk',
    '/api/platform/legal-pages/cookies-extra',
    '/api/platform/legal-pages/cookies/extra',
    '/api/platform/admin/legal-pages/cookies',
    '/api/auth/login',
    '/api/users/me',
    '/api/speed-reading/cms/pages',
  ]) {
    assert.equal(googleAllows(blocked), false, `${blocked} must stay disallowed`);
  }

  const apiAllows = directives.filter((line) => /^Allow:\s*\/api/i.test(line));
  assert.deepEqual(apiAllows, ['Allow: /api/platform/legal-pages/cookies$']);
});
