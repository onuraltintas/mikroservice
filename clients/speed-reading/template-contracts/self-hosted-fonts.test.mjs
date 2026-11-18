import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

// Fonts and icon fonts are bundled from npm (@fontsource), so no page asks Google or a CDN for them.
const clients = new URL('../../', import.meta.url);
const files = [
  'speed-reading/src/index.html',
  'speed-reading/nginx.conf',
  'speed-reading/src/app/features/error/no-subscription.component.ts',
  'admin-panel/src/index.html',
  'admin-panel/projects/staff-portal/src/index.html',
];

test('no client page loads fonts or icon fonts from Google or a CDN', () => {
  for (const file of files) {
    const text = readFileSync(new URL(file, clients), 'utf8');
    assert.doesNotMatch(text, /fonts\.googleapis\.com|fonts\.gstatic\.com|cdnjs\.cloudflare\.com/, file);
  }
});

test('both apps bundle the fonts they use from @fontsource', () => {
  for (const app of ['speed-reading', 'admin-panel']) {
    const config = readFileSync(new URL(`${app}/angular.json`, clients), 'utf8');
    for (const pkg of ['@fontsource/inter/400.css', '@fontsource/material-icons/index.css']) {
      assert.ok(config.includes(pkg), `${app}: ${pkg}`);
    }
  }
});
