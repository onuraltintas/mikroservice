import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const read = (path) => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');

const robots = read('clients/speed-reading/public/robots.txt');

// The four dedicated legal pages and the exact public API path each one reads.
const LEGAL_PAGES = [
  { component: 'cookies/cookies.component.ts', slug: 'cookies' },
  { component: 'privacy/privacy.component.ts', slug: 'privacy' },
  { component: 'kvkk/kvkk.component.ts', slug: 'kvkk' },
  { component: 'terms/terms.component.ts', slug: 'speed-reading-terms' },
];
const ALLOWED_API_PATHS = LEGAL_PAGES.map(({ slug }) => `/api/platform/legal-pages/${slug}`);
const EXPECTED_ALLOW_LINES = ALLOWED_API_PATHS.map((path) => `Allow: ${path}$`);

// Groups as Google reads them: consecutive user-agent lines share the rules below them.
function parseGroups(text) {
  const groups = [];
  let current = null;
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.replace(/#.*/, '').trim();
    const match = line.match(/^([A-Za-z-]+)\s*:\s*(.*)$/);
    if (!match) continue;
    const field = match[1].toLowerCase();
    const value = match[2].trim();
    if (field === 'user-agent') {
      if (!current || current.rules.length) {
        current = { agents: [], rules: [] };
        groups.push(current);
      }
      current.agents.push(value.toLowerCase());
    } else if ((field === 'allow' || field === 'disallow') && current) {
      current.rules.push({ allow: field === 'allow', pattern: value, line: `${field === 'allow' ? 'Allow' : 'Disallow'}: ${value}` });
    }
  }
  return groups;
}

// Googlebot obeys only the most specific matching group(s); `*` applies only when
// no group names Googlebot. All groups naming the same agent are merged.
function rulesFor(groups, agent) {
  const named = groups.filter((group) => group.agents.some((name) => name !== '*' && agent.toLowerCase().startsWith(name)));
  const chosen = named.length ? named : groups.filter((group) => group.agents.includes('*'));
  return chosen.flatMap((group) => group.rules);
}

// Longest matching rule wins; on a tie Allow wins. `*` is a wildcard, a trailing `$` anchors the end.
function allows(rules, url) {
  let best = { length: -1, allow: true };
  for (const rule of rules) {
    if (!rule.pattern) continue;
    const anchored = rule.pattern.endsWith('$');
    const body = anchored ? rule.pattern.slice(0, -1) : rule.pattern;
    const regex = new RegExp(
      '^' + body.split('*').map((part) => part.replace(/[.+?^${}()|[\]\\]/g, '\\$&')).join('.*') + (anchored ? '$' : ''),
    );
    if (!regex.test(url)) continue;
    if (rule.pattern.length > best.length || (rule.pattern.length === best.length && rule.allow)) {
      best = { length: rule.pattern.length, allow: rule.allow };
    }
  }
  return best.allow;
}

const STILL_BLOCKED = [
  '/api/',
  '/api/platform/legal-pages',
  '/api/platform/legal-pages/',
  '/api/platform/legal-pages/accessibility-policy',
  '/api/platform/legal-pages/coaching-terms',
  '/api/platform/legal-pages/speed-reading-newsletter-consent',
  '/api/platform/admin/legal-pages',
  '/api/platform/admin/legal-pages/cookies',
  '/api/platform/admin/legal-pages/privacy',
  '/api/auth/login',
  '/api/users/me',
  '/api/speed-reading/cms/pages',
  ...ALLOWED_API_PATHS.flatMap((path) => [`${path}-extra`, `${path}/extra`, `${path}?v=1`, `${path}.json`]),
];

function assertCrawlPolicy(text) {
  const groups = parseGroups(text);
  for (const agent of ['Googlebot', 'Googlebot-Image', '*']) {
    const rules = rulesFor(groups, agent);
    assert.ok(rules.some((rule) => rule.line === 'Disallow: /api/'), `${agent}: the generic API block must stay in place`);
    for (const path of ALLOWED_API_PATHS) {
      assert.equal(allows(rules, path), true, `${agent} must be allowed to fetch ${path}`);
    }
    for (const page of ['/legal/cookies', '/legal/privacy', '/legal/kvkk', '/legal/terms']) {
      assert.equal(allows(rules, page), true, `${agent} must be allowed to fetch ${page}`);
    }
    for (const blocked of STILL_BLOCKED) {
      assert.equal(allows(rules, blocked), false, `${agent}: ${blocked} must stay disallowed`);
    }
  }
  for (const group of groups) {
    const apiAllows = group.rules.filter((rule) => rule.allow && rule.pattern.startsWith('/api')).map((rule) => rule.line);
    assert.deepEqual(apiAllows.slice().sort(), EXPECTED_ALLOW_LINES.slice().sort(), `group ${group.agents.join(', ')} must allow only the four legal policy paths under /api/`);
  }
}

test('each dedicated legal page reads its text from its exact shared legal-pages endpoint', () => {
  const service = read('clients/speed-reading/src/app/core/services/platform-legal-pages.service.ts');
  const environment = read('clients/speed-reading/src/environments/environment.prod.ts');

  assert.match(environment, /apiUrl:\s*'\/api'/);
  assert.match(service, /\$\{environment\.apiUrl\}\/platform\/legal-pages/);
  for (const { component, slug } of LEGAL_PAGES) {
    const source = read(`clients/speed-reading/src/app/features/legal/${component}`);
    assert.match(source, new RegExp(`getPage\\('${slug}'\\)`), `${component} must read the ${slug} slug`);
  }
});

test('robots.txt lets Googlebot fetch only the four public legal policy API paths', () => {
  assertCrawlPolicy(robots);
});

test('the robots check fails when a Googlebot group drops the legal Allow lines', () => {
  const withBareGooglebotGroup = `${robots}\nUser-agent: Googlebot\nDisallow: /api/\n`;
  assert.throws(() => assertCrawlPolicy(withBareGooglebotGroup), /must be allowed to fetch|must allow only/);

  const withWideAllow = robots.replace('Disallow: /api/', 'Disallow: /api/\nAllow: /api/platform/');
  assert.throws(() => assertCrawlPolicy(withWideAllow), /must stay disallowed|must allow only/);
});
