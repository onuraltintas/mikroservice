import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';

const read = path => readFileSync(new URL(`../../../${path}`, import.meta.url), 'utf8');

// Live check 19.11.2026: masterhizliokuma.com sent no Content-Security-Policy, because nginx drops the server's
// add_header lines in a location that sets any header of its own. The headers now live in one snippet that the server
// and every such location include; the snippet allows only the stylesheet loader Angular writes into index.html.
const sites = [
  { nginx: 'clients/speed-reading/nginx.conf', snippet: 'clients/speed-reading/nginx-security-headers.conf', dockerfile: 'clients/speed-reading/Dockerfile' },
  { nginx: 'clients/admin-panel/projects/staff-portal/nginx.conf', snippet: 'clients/admin-panel/projects/staff-portal/nginx-security-headers.conf', dockerfile: 'clients/admin-panel/Dockerfile.staff-portal' }
];

for (const site of sites) {
  test(`${site.nginx} sends its security headers on every page and file`, () => {
    const nginx = read(site.nginx);
    const include = 'include /etc/nginx/snippets/security-headers.conf;';
    assert.doesNotMatch(nginx, /add_header (Content-Security-Policy|X-Frame-Options)/, 'security headers live only in the snippet');
    assert.ok(nginx.indexOf(include) < nginx.indexOf('\n    location '), 'the server includes the snippet');
    for (const block of nginx.split(/\n\s*location /).slice(1)) {
      if (/add_header (?!Content-Type)/.test(block)) assert.ok(block.includes(include), `a location with its own headers includes the snippet:\n${block.slice(0, 80)}`);
    }
    const snippet = read(site.snippet);
    assert.match(snippet, /add_header Content-Security-Policy "default-src 'self';[^"]*script-src 'self' 'unsafe-hashes' 'sha256-MhtPZXr7\+LpJUY5qtMutB\+qWfQtMaPccfe7QXtCcEYc='/);
    assert.match(snippet, /https:\/\/www\.google\.com\/recaptcha\//, 'reCAPTCHA can load');
    assert.match(read(site.dockerfile), new RegExp(`COPY ${site.snippet.replace(/^clients\//, '').replace(/[.]/g, '\\.')} /etc/nginx/snippets/security-headers\\.conf`));
  });
}
