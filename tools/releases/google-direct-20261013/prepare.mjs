import { createHash } from 'node:crypto';
import { cp, mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const directory = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(directory, '../../..');
await mkdir(path.join(directory, 'patch'), { recursive: true });
await cp(path.join(root, 'clients/speed-reading/.auth-timeout-release-build-20261013-r1/dist/speed-reading-frontend/browser'), path.join(directory, 'web/browser'), { recursive: true });
for (const name of ['Identity.Application.dll', 'Identity.Application.pdb']) {
  await cp(path.join(directory, 'application', name), path.join(directory, 'patch', name));
}
const files = ['Dockerfile.identity', 'Dockerfile.frontend', 'production.override.yml', 'deploy.sh', ...(await listFiles('patch')), ...(await listFiles('probe-runtime')), ...(await listFiles('web'))];
const manifest = [];
for (const file of files.sort()) {
  manifest.push(`${createHash('sha256').update(await readFile(path.join(directory, file))).digest('hex')}  ${file}`);
}
await writeFile(path.join(directory, 'payload.sha256'), manifest.join('\n') + '\n');

async function listFiles(relative) {
  const result = [];
  for (const entry of await readdir(path.join(directory, relative), { withFileTypes: true })) {
    const file = `${relative}/${entry.name}`;
    if (entry.isDirectory()) result.push(...await listFiles(file));
    else result.push(file);
  }
  return result;
}
