// Reuse the verified frontend-only deployment procedure; no API or SQL payload.
import { readFileSync, writeFileSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const output = resolve(root, 'artifacts/pending-preview-age-20261011');
const original = resolve(root, 'tools/releases/attention-20261011');
for (const file of ['deploy.sh', 'web.Dockerfile', 'production.override.yml']) {
  const text = readFileSync(resolve(original, file), 'utf8').replaceAll('\r\n', '\n')
    .replaceAll('attention-20261011-catalogue', 'preview-age-20261011')
    .replaceAll('attention release:', 'preview age contract release:');
  writeFileSync(resolve(output, file), text);
}
writeFileSync(resolve(output, 'nginx.conf'), readFileSync(resolve(root, 'clients/speed-reading/nginx.conf'), 'utf8').replaceAll('\r\n', '\n'));
writeFileSync(resolve(output, 'source-revision.txt'), execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim() + '\n');
const walk = directory => readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
  if (entry.isSymbolicLink()) throw new Error('Payload cannot contain symlinks');
  const path = resolve(directory, entry.name);
  return entry.isDirectory() ? walk(path) : [path];
});
const files = walk(output).filter(file => file !== resolve(output, 'web.sha256'));
if (files.some(file => file.endsWith('.sql') || file.endsWith('.env'))) throw new Error('Frontend patch cannot contain SQL or environment files');
writeFileSync(resolve(output, 'web.sha256'), files.sort().map(file =>
  `${createHash('sha256').update(readFileSync(file)).digest('hex')}  ${file.slice(output.length + 1).replaceAll('\\', '/')}`
).join('\n') + '\n');
console.log(`Sealed frontend-only patch: ${files.length} files.`);
