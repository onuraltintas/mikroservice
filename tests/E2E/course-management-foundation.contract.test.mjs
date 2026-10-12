import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = fileURLToPath(new URL('../../', import.meta.url));
const read = path => readFileSync(resolve(root, path), 'utf8');
const baseline = JSON.parse(read('tests/E2E/support/course-management-protected-baseline.json'));

test('existing product implementation files remain byte-for-byte unchanged', () => {
  for (const [path, hash] of Object.entries(baseline.files)) {
    const actual = createHash('sha256').update(readFileSync(resolve(root, path))).digest('hex');
    assert.equal(actual, hash, `Protected product file changed: ${path}`);
  }
});

test('existing gateway routes and destinations retain their configuration', () => {
  const proxy = JSON.parse(read('services/api-gateway/appsettings.json')).ReverseProxy;
  for (const [name, route] of Object.entries(baseline.routes)) assert.deepEqual(proxy.Routes[name], route);
  for (const [name, cluster] of Object.entries(baseline.clusters)) assert.deepEqual(proxy.Clusters[name], cluster);
});

test('new product has a separate gateway destination and namespaced API route', () => {
  const proxy = JSON.parse(read('services/api-gateway/appsettings.json')).ReverseProxy;
  assert.equal(proxy.Routes['course-management-route']?.Match.Path, '/api/course-management/{**catch-all}');
  assert.equal(proxy.Routes['course-management-route']?.ClusterId, 'course-management-cluster');
  assert.equal(proxy.Clusters['course-management-cluster']?.Destinations.destination1.Address, 'http://localhost:5005');
});

test('student application is separate and does not import another product', () => {
  const config = JSON.parse(read('clients/course-management/angular.json'));
  assert.ok(config.projects['course-management-student']);
  const source = read('clients/course-management/src/app/app.component.ts');
  assert.doesNotMatch(source, /admin-panel|speed-reading|coaching-portal/);
});
