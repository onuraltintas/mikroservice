import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

test('gateway exposes only GET /api/data-privacy/export to Coaching', async () => {
  const config = JSON.parse(await readFile(
    new URL('../../services/api-gateway/appsettings.json', import.meta.url),
    'utf8',
  ));
  const route = config.ReverseProxy.Routes['coaching-data-export-route'];

  assert.ok(route, 'Coaching data export must have a gateway route');
  assert.equal(route.ClusterId, 'coaching-cluster');
  assert.equal(route.Match.Path, '/api/data-privacy/export');
  assert.deepEqual(route.Match.Methods, ['GET']);
  assert.notEqual(route.AuthorizationPolicy, 'Anonymous');
});
