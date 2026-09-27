import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const compose = readFileSync(new URL('../../docker-compose.production.yml', import.meta.url), 'utf8');
const service = compose.split('  speed-reading-service:')[1]?.split('  speed-reading-migrations:')[0] ?? '';

test('production speed-reading service receives required RabbitMQ settings', () => {
  assert.match(service, /RABBITMQ_HOST:\s*rabbitmq/);
  assert.match(service, /RABBITMQ_DEFAULT_USER:\s*\$\{RABBITMQ_DEFAULT_USER/);
  assert.match(service, /RABBITMQ_DEFAULT_PASS:\s*\$\{RABBITMQ_DEFAULT_PASS/);
});
