import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { checkCoachingStorage } from '../../../tools/coaching-storage-preflight.mjs';

function configuration({ mountType = 'bind', ports = [], appKey = 'coaching-key', rootKey = 'root-key' } = {}) {
  return {
    services: {
      minio: {
        ports,
        volumes: [{ type: mountType, source: '/srv/eduivme/storage', target: '/data' }],
        environment: { MINIO_ROOT_USER: rootKey }
      },
      'coaching-service': {
        environment: {
          Coaching__Attachments__Provider: 'Minio',
          Coaching__Attachments__MinioAccessKey: appKey
        }
      }
    }
  };
}

test('accepts private bind-mounted storage with a distinct application account', () => {
  assert.deepEqual(checkCoachingStorage(configuration()), []);
});

test('rejects root credentials used by Coaching without printing credentials', () => {
  assert.deepEqual(checkCoachingStorage(configuration({ appKey: 'shared-secret', rootKey: 'shared-secret' })), [
    'Coaching object-storage account must differ from MinIO root account.'
  ]);
});

test('rejects a Docker volume in place of a verified host bind mount', () => {
  assert.deepEqual(checkCoachingStorage(configuration({ mountType: 'volume' })), [
    'MinIO /data must use a host bind mount.'
  ]);
});

test('rejects publicly published MinIO ports', () => {
  assert.deepEqual(checkCoachingStorage(configuration({ ports: ['9000:9000'] })), [
    'MinIO must not publish ports to the host.'
  ]);
});

test('CI validates the production storage configuration before release', async () => {
  const workflow = await readFile(new URL('../../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  const productionStep = workflow.split('      - name: Validate production and observability overlays')[1]
    ?.split('      - name: Validate monitoring configuration files')[0];
  assert.match(productionStep, /node tools\/coaching-storage-preflight\.mjs/);
  assert.match(productionStep, /ATTACHMENT_STORAGE_PROVIDER: Minio/);
});
