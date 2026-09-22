import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const root = new URL('../../../', import.meta.url);

async function read(path) {
  return readFile(new URL(path, root), 'utf8');
}

test('production provisions a least-privilege Coaching object-storage account before Coaching starts', async () => {
  const [base, production, policy] = await Promise.all([
    read('docker-compose.yml'),
    read('docker-compose.production.yml'),
    read('infrastructure/minio/coaching-attachments-policy.json')
  ]);

  assert.match(base, /^  minio-provision:/m);
  assert.match(base, /ATTACHMENT_MINIO_ACCESS_KEY/);
  assert.match(base, /ATTACHMENT_MINIO_SECRET_KEY/);
  assert.match(base, /mc admin policy create local coaching-attachments/m);
  assert.match(base, /mc admin user add local "\$ATTACHMENT_MINIO_ACCESS_KEY"/m);
  assert.match(base, /mc admin policy attach local coaching-attachments --user="\$ATTACHMENT_MINIO_ACCESS_KEY"/m);

  assert.match(production, /source: \$\{ATTACHMENT_MINIO_DATA_HOST_PATH:\?/);
  assert.match(production, /minio-provision:\n[\s\S]*profiles: !reset \[\]/);
  assert.match(production, /minio-provision:\n[\s\S]*condition: service_completed_successfully/);

  const parsed = JSON.parse(policy);
  assert.deepEqual(parsed.Statement[0].Action, [
    's3:GetBucketLocation',
    's3:ListBucket'
  ]);
  assert.deepEqual(parsed.Statement[1].Action, [
    's3:AbortMultipartUpload',
    's3:DeleteObject',
    's3:GetObject',
    's3:PutObject'
  ]);
  assert.deepEqual(parsed.Statement[0].Resource, ['arn:aws:s3:::${ATTACHMENT_MINIO_BUCKET}']);
  assert.deepEqual(parsed.Statement[1].Resource, ['arn:aws:s3:::${ATTACHMENT_MINIO_BUCKET}/*']);
});
