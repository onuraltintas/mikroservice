import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import test from 'node:test';

// Live test 19.11.2026 (A6, decision K2): the service worker produced false 504s and served the previous release.
const read = path => readFileSync(new URL(`../${path}`, import.meta.url), 'utf8');

test('the application no longer registers an Angular service worker', () => {
  assert.doesNotMatch(read('src/app/app.config.ts'), /provideServiceWorker/);
  assert.doesNotMatch(read('angular.json'), /"serviceWorker"/);
  assert.equal(existsSync(new URL('../ngsw-config.json', import.meta.url)), false);
  assert.doesNotMatch(read('package.json'), /@angular\/service-worker/);
});

test('browsers with the old worker get a worker that clears caches and removes itself', () => {
  const worker = read('public/ngsw-worker.js');
  assert.match(worker, /skipWaiting\(\)/);
  assert.match(worker, /caches\.delete/);
  assert.match(worker, /registration\.unregister\(\)/);
  assert.doesNotMatch(worker, /addEventListener\('fetch'/);
});

test('the removal worker is never cached by the browser', () => {
  const nginx = read('nginx.conf');
  const block = nginx.split('location ~ ^/(?:ngsw-worker\\.js|manifest\\.webmanifest)$')[1]?.split('}')[0] ?? '';
  assert.match(block, /no-cache, no-store, must-revalidate/);
});
