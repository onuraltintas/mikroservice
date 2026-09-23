import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { checkCoachingStorage } from '../../../tools/coaching-storage-preflight.mjs';

function configuration({ provider = 'Local', volumes } = {}) {
  return {
    services: {
      'coaching-service': {
        environment: {
          Coaching__Attachments__Provider: provider,
          Coaching__Attachments__RootPath: '/var/lib/eduplatform/attachments',
          Coaching__Attachments__Scanner__Provider: 'ClamAv'
        },
        volumes: volumes ?? [{ type: 'volume', source: 'coaching_attachments', target: '/var/lib/eduplatform/attachments' }]
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

test('rejects an unexpected storage provider', () => {
  assert.deepEqual(checkCoachingStorage(configuration({ provider: 'External' })), [
    'Production Coaching must use local attachment storage.'
  ]);
});

test('CI validates the local production storage configuration', async () => {
  const workflow = await readFile(new URL('../../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  const productionStep = workflow.split('      - name: Validate production and observability overlays')[1]
    ?.split('      - name: Validate monitoring configuration files')[0];
  assert.match(productionStep, /node tools\/coaching-storage-preflight\.mjs/);
  assert.match(productionStep, /docker compose[\s\S]*config --format json \| node tools\/coaching-storage-preflight\.mjs/);
});
