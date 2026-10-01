import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const vhost = readFileSync(
  new URL('../../../infrastructure/litespeed/eduivme.com.vhost.conf', import.meta.url),
  'utf8',
);

test('SignalR HTTP transports use the production edge at the hub root', () => {
  const context = vhost.match(/context\s+\/hubs\/notifications\s*\{([^}]*)\}/s);

  assert.ok(context, 'an explicit HTTP context is required for the SignalR hub root');
  assert.match(context[1], /type\s+proxy/);
  assert.match(context[1], /handler\s+eduivme_production_edge/);
});

test('SignalR negotiation and WebSocket routes remain configured', () => {
  assert.match(vhost, /context\s+\/hubs\/notifications\/negotiate\s*\{/);
  assert.match(vhost, /websocket\s+\/hubs\/notifications\s*\{/);
});
