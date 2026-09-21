import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const files = [
  'HealthCheckTests.cs',
  'DatabaseCrudTests.cs',
  'EventPublishingTests.cs',
  'RedisCacheTests.cs',
];

for (const file of files) {
  test(`${file} does not log credential-bearing connection strings`, async () => {
    const source = await readFile(new URL(file, import.meta.url), 'utf8');
    assert.doesNotMatch(source, /_output\.WriteLine\([^\n]*ConnectionString/);
  });
}
