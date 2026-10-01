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
    const coaching = overlay.split('\n  coaching-service:\n')[1]?.split('\n  coaching-migrations:\n    image:')[0];
    assert.ok(coaching);
    assert.match(coaching, /Coaching__Attachments__Provider: Local/);
    assert.match(coaching, /Coaching__Attachments__Scanner__Provider: ClamAv/);
  }

  const productionCoaching = production.split('\n  coaching-service:\n')[1]?.split('\n  coaching-migrations:\n    image:')[0];
  assert.ok(productionCoaching);
  assert.match(productionCoaching, /Coaching__CmsMedia__RootPath: \/var\/lib\/eduplatform\/coaching-cms-media/);
  assert.match(productionCoaching, /coaching_cms_media:\/var\/lib\/eduplatform\/coaching-cms-media/);

  const productionMigration = production.split('\n  coaching-migrations:\n')[1]?.split('\n  speed-reading-service:')[0];
  assert.ok(productionMigration);
  assert.match(productionMigration, /Coaching__Attachments__Provider: Local/);
  assert.match(productionMigration, /Coaching__Attachments__RootPath: \/var\/lib\/eduplatform\/attachments/);
  assert.match(productionMigration, /Coaching__Attachments__Scanner__Provider: ClamAv/);
  assert.match(productionMigration, /Coaching__CmsMedia__RootPath: \/var\/lib\/eduplatform\/coaching-cms-media/);
});
