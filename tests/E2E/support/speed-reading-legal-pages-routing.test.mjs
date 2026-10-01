import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const caddyfile = readFileSync(
  new URL('../../../infrastructure/caddy/Caddyfile.speed-reading.litespeed', import.meta.url),
  'utf8',
);

test('Speed Reading forwards only public legal-page reads to the Identity Gateway', () => {
  const route = caddyfile.match(
    /@sharedLegalPages\s*\{([^}]*)\}\s*handle\s+@sharedLegalPages\s*\{([^}]*)\}/s,
  );

  assert.ok(route, 'the Speed Reading edge must explicitly route shared legal pages');
  assert.match(route[1], /method\s+GET/);
  assert.match(route[1], /path[^}]*\/api\/platform\/legal-pages\/\*/s);
  assert.match(route[2], /reverse_proxy\s+api-gateway:8080/);
  assert.doesNotMatch(route[0], /\/api\/platform\/admin\/legal-pages/);
  assert.match(caddyfile, /@blockedApi\s+path\s+\/api\/\*/);
});
