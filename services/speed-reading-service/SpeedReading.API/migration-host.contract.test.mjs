import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const program = await readFile(new URL('./Program.cs', import.meta.url), 'utf8');
const compose = await readFile(new URL('../../../docker-compose.yml', import.meta.url), 'utf8');

test('migration host registers the HTTP context dependency before building', () => {
  const registration = program.indexOf('builder.Services.AddHttpContextAccessor();');
  const migrationBranch = program.indexOf('if (migrationOnly)');

  assert.notEqual(registration, -1);
  assert.ok(registration < migrationBranch);
});

test('speed reading API receives RabbitMQ credentials from compose', () => {
  const service = compose
    .split('  speed-reading-service:')[1]
    .split('  speed-reading-frontend:')[0];

  assert.match(service, /RabbitMQ__Host=rabbitmq/);
  assert.match(service, /RabbitMQ__Username=\$\{RABBITMQ_DEFAULT_USER\}/);
  assert.match(service, /RabbitMQ__Password=\$\{RABBITMQ_DEFAULT_PASS\}/);
});
