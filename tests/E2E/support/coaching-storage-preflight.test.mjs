import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { checkCoachingStorage } from '../../../tools/coaching-storage-preflight.mjs';

function configuration({ provider = 'Local', volumes, dependsOn = {} } = {}) {
  return {
    services: {
      'coaching-service': {
        environment: {
          Coaching__Attachments__Provider: provider,
          Coaching__Attachments__RootPath: '/var/lib/eduplatform/attachments',
          Coaching__Attachments__Scanner__Provider: 'ClamAv'
        },
        volumes: volumes ?? [{ type: 'volume', source: 'coaching_attachments', target: '/var/lib/eduplatform/attachments' }],
        depends_on: dependsOn
      }
    }
  };
}

test('accepts local Coaching storage mounted on the VPS', () => {
  assert.deepEqual(checkCoachingStorage(configuration()), []);
});

test('rejects unmounted attachment storage', () => {
  assert.deepEqual(checkCoachingStorage(configuration({ volumes: [] })), [
    'Coaching attachment path must be backed by a persistent mount.'
  ]);
});

test('rejects a MinIO dependency in the production Coaching service', () => {
  assert.deepEqual(checkCoachingStorage(configuration({ dependsOn: { minio: { condition: 'service_healthy' } } })), [
    'Production Coaching must not depend on MinIO.'
  ]);
});

test('rejects an unexpected storage provider', () => {
  assert.deepEqual(checkCoachingStorage(configuration({ provider: 'Minio' })), [
    'Production Coaching must use local attachment storage.'
  ]);
});

test('CI validates the local production storage configuration', async () => {
  const workflow = await readFile(new URL('../../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  const productionStep = workflow.split('      - name: Validate production and observability overlays')[1]
    ?.split('      - name: Validate monitoring configuration files')[0];
  assert.match(productionStep, /node tools\/coaching-storage-preflight\.mjs/);
  assert.doesNotMatch(productionStep, /ATTACHMENT_STORAGE_PROVIDER: Minio/);
});
