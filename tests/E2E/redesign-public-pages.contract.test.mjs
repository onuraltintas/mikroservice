import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

// Redesign 18.11.2026: maxkoc.com opens its public page, eduivme.com has its own page instead of forwarding.
const read = (file) => readFileSync(new URL(`../../${file}`, import.meta.url), 'utf8');
const caddy = read('infrastructure/caddy/Caddyfile.production.litespeed');
const block = (head) => {
  const start = caddy.indexOf(`\n${head} {`);
  assert.ok(start >= 0, head);
  let depth = 0;
  for (let i = caddy.indexOf('{', start); i < caddy.length; i++) {
    if (caddy[i] === '{') depth++;
    if (caddy[i] === '}' && --depth === 0) return caddy.slice(start, i + 1);
  }
  throw new Error(head);
};

test('maxkoc.com "/" opens the public Koçluk page, not the sign-in page', () => {
  const site = block('maxkoc.com:80');
  assert.match(site, /@root path \/\n\s*redir @root \/coaching 302/);
  assert.doesNotMatch(site, /redir @root \/coaching-portal/);
});

test('eduivme.com serves its own page and still sends sign-in, management and Koçluk addresses where they belong', () => {
  const site = block(':80');
  assert.match(site, /@management path \/auth \/auth\/\* \/dashboard \/dashboard\/\*[^\n]*\n\s*handle @management \{\n\s*redir https:\/\/admin\.eduivme\.com\{uri\} 302/);
  assert.match(site, /redir https:\/\/maxkoc\.com\{uri\} 302/);
  assert.match(site, /redir https:\/\/ogretmen\.eduivme\.com\{uri\} 302/);
  assert.match(site, /handle \{\n\s*reverse_proxy admin-panel:4000\n\s*\}/);
  const routes = read('clients/admin-panel/src/app/app.routes.ts');
  assert.match(routes, /canMatch: \[eduivmeHomeHost\],\s*loadComponent: \(\) => import\('\.\/features\/public\/eduivme-home\.component'\)/);
  assert.ok(routes.indexOf('eduivmeHomeHost],') < routes.indexOf("redirectTo: 'dashboard'"), 'the eduivme page is matched before the dashboard redirect');
  assert.match(read('clients/admin-panel/src/app/features/public/eduivme-home.guard.ts'), /\^\(www\\\.\)\?eduivme\\\.com\$/);
});

test('the public pages carry their brand and the company line', () => {
  const layout = read('clients/admin-panel/src/app/features/coaching-cms/layout/coaching-public-cms-layout.component.ts');
  assert.match(layout, /data-brand="maxkoc"/);
  assert.match(layout, /ONAL Yazılım ve Otomasyon\. Tüm hakları saklıdır\./);
  const home = read('clients/admin-panel/src/app/features/public/eduivme-home.component.ts');
  assert.match(home, /data-brand="eduivme"/);
  // eduivme.com (19.11.2026): the frame of every page carries the brand and the company line.
  const frame = read('clients/admin-panel/src/app/features/public/eduivme/eduivme-site-layout.component.ts');
  assert.match(frame, /data-brand="eduivme"/);
  assert.match(frame, /ONAL Yazılım ve Otomasyon\. Tüm hakları saklıdır\./);
});

test('eduivme.com reads its content from site-service on its own address and shows product pages', () => {
  const site = block(':80');
  const content = site.indexOf('@siteContent path /api/site/*');
  assert.ok(content > 0 && content < site.indexOf('@management path'), 'the site content route comes before the management redirect');
  assert.match(site, /handle @siteContent \{\n\s*reverse_proxy api-gateway:8080/);
  const routes = read('clients/admin-panel/src/app/app.routes.ts');
  assert.match(routes, /path: 'urunler\/:slug',\s*canMatch: \[eduivmeHomeHost\]/);
  const gateway = JSON.parse(read('services/api-gateway/appsettings.json'));
  assert.equal(gateway.ReverseProxy.Routes['site-public-route'].AuthorizationPolicy, 'Anonymous');
  assert.equal(gateway.ReverseProxy.Routes['site-public-route'].Match.Path, '/api/site/{**catch-all}');
  assert.equal(gateway.ReverseProxy.Routes['site-admin-route'].AuthorizationPolicy, undefined);
});
