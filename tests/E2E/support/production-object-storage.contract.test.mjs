import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const root = new URL('../../../', import.meta.url);

async function read(path) {
  return readFile(new URL(path, root), 'utf8');
}

test('production provisions a least-privilege Coaching object-storage account before Coaching starts', async () => {
  const [base, ci, production, policy, provisioner] = await Promise.all([
    read('docker-compose.yml'),
    read('.github/workflows/ci.yml'),
    read('docker-compose.production.yml'),
    read('infrastructure/minio/coaching-attachments-policy.json'),
    read('infrastructure/minio/provision-coaching-attachments.sh')
  ]);

  assert.match(base, /^  minio-provision:/m);
  assert.match(base, /ATTACHMENT_MINIO_ACCESS_KEY/);
  assert.match(base, /ATTACHMENT_MINIO_SECRET_KEY/);
  assert.match(provisioner, /mc admin policy create local coaching-attachments/m);
  assert.match(provisioner, /mc admin user add local "\$ATTACHMENT_MINIO_ACCESS_KEY"/m);
  assert.match(provisioner, /mc admin policy attach local coaching-attachments --user="\$ATTACHMENT_MINIO_ACCESS_KEY"/m);

  assert.match(production, /source: \$\{ATTACHMENT_MINIO_DATA_HOST_PATH:\?/);
  assert.match(production, /minio-provision:\n[\s\S]*profiles: !reset \[\]/);
  assert.match(production, /minio-provision:\n[\s\S]*condition: service_completed_successfully/);
  assert.match(ci, /production-object-storage\.contract\.test\.mjs/);

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
  assert.deepEqual(parsed.Statement[0].Resource, ['arn:aws:s3:::eduplatform-attachments']);
  assert.deepEqual(parsed.Statement[1].Resource, ['arn:aws:s3:::eduplatform-attachments/*']);
});

test('production Coaching uses its persistent local volume without starting MinIO', async () => {
  const [base, production] = await Promise.all([
    read('docker-compose.yml'),
    read('docker-compose.production.yml')
  ]);

  const coaching = production.split('  coaching-service:')[1]?.split('  coaching-migrations:')[0];
  const minio = production.split('  minio:')[1]?.split('  minio-provision:')[0];
  assert.match(base, /coaching_attachments:\/var\/lib\/eduplatform\/attachments/);
  assert.match(coaching, /Coaching__Attachments__Provider: Local/);
  assert.doesNotMatch(coaching, /^      minio:/m);
  assert.doesNotMatch(coaching, /^      minio-provision:/m);
  assert.match(minio, /profiles: \["object-storage"\]/);
});
