import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

// eduivme.com's legal pages read /api/platform/legal-pages/* on eduivme.com itself (27.11.2026). The catch-all
// eduivme.com block redirects other /api paths to admin.eduivme.com; that redirect must not catch these reads,
// because the browser blocks the credentialed cross-origin response and the pages show "not published".
const caddyfile = readFileSync(new URL('../../infrastructure/caddy/Caddyfile.production.litespeed', import.meta.url), 'utf8');

function eduivmeBlock(text) {
  const start = text.search(/^:80 \{$/m);
  assert.ok(start >= 0, 'catch-all :80 block (eduivme.com) is present');
  return text.slice(start);
}

test('eduivme.com proxies the public legal documents before redirecting /api to the management address', () => {
  const block = eduivmeBlock(caddyfile);
  const legal = block.indexOf('@sharedLegalPages {');
  const management = block.indexOf('@management path');
  assert.ok(legal >= 0, 'the legal-pages matcher exists in the eduivme.com block');
  assert.ok(management > legal, 'the legal-pages handler comes before the management redirect');
  const matcher = block.slice(legal, block.indexOf('}', legal));
  assert.match(matcher, /method GET/);
  assert.match(matcher, /path \/api\/platform\/legal-pages \/api\/platform\/legal-pages\/\*/);
  const handler = block.slice(block.indexOf('handle @sharedLegalPages'), management);
  assert.match(handler, /reverse_proxy api-gateway:8080/);
  assert.doesNotMatch(handler, /redir/);
});

test('admin legal-page management stays on the management address', () => {
  const block = eduivmeBlock(caddyfile);
  const matcher = block.slice(block.indexOf('@sharedLegalPages {'), block.indexOf('handle @sharedLegalPages'));
  assert.doesNotMatch(matcher, /admin/);
});
