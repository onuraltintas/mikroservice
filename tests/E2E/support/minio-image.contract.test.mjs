import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

test('Compose uses the verified Quay MinIO image and pinned release', async () => {
  const compose = await readFile(new URL('../../../docker-compose.yml', import.meta.url), 'utf8');
  assert.match(compose, /image: quay\.io\/minio\/minio:RELEASE\.2025-04-22T22-12-26Z/);
  assert.doesNotMatch(compose, /image: minio\/minio:/);
});
