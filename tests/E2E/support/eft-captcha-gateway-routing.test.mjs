import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const routes = JSON.parse(readFileSync(new URL('../../../services/api-gateway/appsettings.json', import.meta.url), 'utf8')).ReverseProxy.Routes;
for (const [path, cluster] of [
  ['/api/coaching/subscriptions/recaptcha', 'coaching-cluster'],
  ['/api/speed-reading/bank-transfer/recaptcha', 'speed-reading-cluster']
]) {
  test(path + ' permits only anonymous GET configuration reads', () => {
    const route = Object.values(routes).find(route => route.Match.Path === path);
    assert.ok(route, 'Dedicated CAPTCHA configuration route is required');
    assert.equal(route.ClusterId, cluster);
    assert.equal(route.AuthorizationPolicy, 'Anonymous');
    assert.deepEqual(route.Match.Methods, ['GET']);
  });
}
