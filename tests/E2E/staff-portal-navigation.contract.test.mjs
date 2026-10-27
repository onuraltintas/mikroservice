import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

// Roadmap O2: the staff portal address depends on the environment and the portal works on the phone.
const root = fileURLToPath(new URL('../../', import.meta.url));
const app = 'clients/admin-panel';
const read = path => readFile(join(root, path), 'utf8');

async function files(dir) {
  const found = [];
  for (const entry of await readdir(join(root, dir), { withFileTypes: true }).catch(() => [])) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) found.push(...await files(path));
    else if (/\.(ts|html)$/.test(entry.name) && !entry.name.includes('.spec.')) found.push(path);
  }
  return found;
}

test('The staff portal address comes from the environment', async () => {
  const hardcoded = [];
  for (const path of await files(`${app}/src/app`)) if ((await read(path)).includes('onuraltintas.net/staff')) hardcoded.push(path);
  assert.deepEqual(hardcoded, [], 'use environment.staffPortalUrl');
  assert.match(await read(`${app}/src/environments/environment.production.ts`), /staffPortalUrl:\s*'https:\/\/onuraltintas\.net\/staff\/'/);
  for (const name of ['environment.ts', 'environment.development.ts'])
    assert.match(await read(`${app}/src/environments/${name}`), /staffPortalUrl:\s*'http:\/\/localhost:4201\/'/);
  const angular = JSON.parse(await read(`${app}/angular.json`));
  assert.equal(angular.projects['staff-portal'].architect.serve.options.port, 4201);
});

test('The staff portal keeps its place in the address', async () => {
  assert.match(await read(`${app}/projects/staff-portal/src/app/app.config.ts`), /StaffLocationService/);
});

test('Staff tables become cards on the phone', async () => {
  const styles = await read(`${app}/projects/staff-portal/src/styles.scss`);
  assert.match(styles, /@media \(max-width: 640px\)[\s\S]*td\[data-label\]::before[\s\S]*content: attr\(data-label\)/);
});
