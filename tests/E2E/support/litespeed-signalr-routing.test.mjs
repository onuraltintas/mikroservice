import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

// Every site that serves the admin-panel/coaching SPA opens the notification hub with Server-Sent
// Events or Long Polling (WebSockets are off in the client). LiteSpeed's `websocket /hubs/notifications`
// directive claims that path, so a plain HTTP GET there gets LiteSpeed's own 404 unless an explicit
// proxy context exists for the hub root (found live on onuraltintas.net, 06.11.2026).
const sites = ['eduivme.com', 'onuraltintas.net'];

for (const site of sites) {
  const vhost = readFileSync(
    new URL(`../../../infrastructure/litespeed/${site}.vhost.conf`, import.meta.url),
    'utf8',
  );

  test(`${site}: SignalR HTTP transports use the production edge at the hub root`, () => {
    const context = vhost.match(/context\s+\/hubs\/notifications\s*\{([^}]*)\}/s);

    assert.ok(context, 'an explicit HTTP context is required for the SignalR hub root');
    assert.match(context[1], /type\s+proxy/);
    assert.match(context[1], /handler\s+eduivme_production_edge/);
  });

  test(`${site}: SignalR negotiation and WebSocket routes remain configured`, () => {
    assert.match(vhost, /context\s+\/hubs\/notifications\/negotiate\s*\{/);
    assert.match(vhost, /websocket\s+\/hubs\/notifications\s*\{/);
  });
}
