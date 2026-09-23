import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const root = new URL('../../../', import.meta.url);
const read = path => readFile(new URL(path, root), 'utf8');

test('Coaching uses persistent VPS-local storage in all Compose environments', async () => {
  const [base, staging, production] = await Promise.all([
    read('docker-compose.yml'),
    read('docker-compose.staging.yml'),
    read('docker-compose.production.yml')
  ]);

  assert.match(base, /coaching_attachments:\/var\/lib\/eduplatform\/attachments/);
  assert.match(base, /Coaching__Attachments__RootPath=\/var\/lib\/eduplatform\/attachments/);
  assert.match(base, /Coaching__Attachments__Provider=Local/);
  for (const overlay of [staging, production]) {
    const coaching = overlay.split('  coaching-service:')[1]?.split('  coaching-migrations:')[0];
    assert.ok(coaching);
    assert.match(coaching, /Coaching__Attachments__Provider: Local/);
    assert.match(coaching, /Coaching__Attachments__Scanner__Provider: ClamAv/);
  }
});
