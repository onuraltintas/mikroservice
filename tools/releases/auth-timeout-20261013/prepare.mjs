import { createHash } from 'node:crypto';
import { copyFile, mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const releaseDir = path.dirname(fileURLToPath(import.meta.url));
const sourceRoot = path.resolve(releaseDir, '..', '..', '..');
const buildRoot = process.argv[2];
if (!buildRoot) throw new Error('Usage: node prepare.mjs <isolated-speed-reading-build-root>');

const browserRoot = path.join(buildRoot, 'dist', 'speed-reading-frontend', 'browser');
const filesToCopy = [
  [path.join(buildRoot, 'nginx.conf'), path.join(releaseDir, 'nginx.conf')],
  [path.join(sourceRoot, 'infrastructure', 'caddy', 'Caddyfile.speed-reading.litespeed'), path.join(releaseDir, 'Caddyfile.speed-reading.litespeed')]
];

await mkdir(path.join(releaseDir, 'web'), { recursive: true });
await copyDirectory(browserRoot, path.join(releaseDir, 'web', 'browser'));
for (const [source, destination] of filesToCopy) await copyFile(source, destination);
await writeFile(path.join(releaseDir, 'source-revision.txt'), '319b250c104539dc55a136077f2e5bf99c6c99e4\n');

const manifestFiles = [
  'Caddyfile.speed-reading.litespeed',
  'nginx.conf',
  'production.override.yml',
  'source-revision.txt',
  'web.Dockerfile',
  ...(await listFiles(path.join(releaseDir, 'web'))).map(file => path.relative(releaseDir, file).replaceAll(path.sep, '/'))
];
const entries = [];
for (const relativePath of manifestFiles.sort()) {
  const content = await readFile(path.join(releaseDir, relativePath));
  const digest = createHash('sha256').update(content).digest('hex');
  entries.push(`${digest}  ${relativePath}`);
}
await writeFile(path.join(releaseDir, 'payload.sha256'), `${entries.join('\n')}\n`);

async function copyDirectory(source, destination) {
  await mkdir(destination, { recursive: true });
  for (const entry of await readdir(source, { withFileTypes: true })) {
    const sourcePath = path.join(source, entry.name);
    const destinationPath = path.join(destination, entry.name);
    if (entry.isDirectory()) await copyDirectory(sourcePath, destinationPath);
    else await copyFile(sourcePath, destinationPath);
  }
}

async function listFiles(directory) {
  const files = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await listFiles(fullPath));
    else files.push(fullPath);
  }
  return files;
}
