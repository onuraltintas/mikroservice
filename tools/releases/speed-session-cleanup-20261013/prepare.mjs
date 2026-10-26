import { copyFileSync, cpSync, existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { dirname, isAbsolute, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const releaseSource = dirname(fileURLToPath(import.meta.url));
const output = resolve(process.argv[2] ?? 'artifacts/speed-session-cleanup-20261013');
const artifactsRoot = resolve(root, 'artifacts');
const outputRelative = relative(artifactsRoot, output);
if (!outputRelative || outputRelative.startsWith(`..${sep}`) || isAbsolute(outputRelative)) {
  throw new Error('Output must be a new directory inside workspace artifacts.');
}
if (existsSync(output)) throw new Error(`Output already exists: ${output}`);

const apiSource = resolve(root, 'services/speed-reading-service/SpeedReading.API/bin/Release/net10.0/publish');
const webSource = resolve(root, 'clients/speed-reading/dist/speed-reading-frontend/browser');
if (!existsSync(resolve(apiSource, 'SpeedReading.API.dll')) || !existsSync(resolve(webSource, 'index.html'))) {
  throw new Error('Build the Speed Reading API and production frontend before preparing this release.');
}

mkdirSync(output, { recursive: false });
cpSync(apiSource, resolve(output, 'api'), { recursive: true, errorOnExist: true });
cpSync(webSource, resolve(output, 'web/browser'), { recursive: true, errorOnExist: true });
for (const file of ['README.md', 'deploy.sh', 'monitor.sh', 'api.Dockerfile', 'web.Dockerfile', 'production.override.yml', 'prepare.mjs']) {
  copyFileSync(resolve(releaseSource, file), resolve(output, file));
}
copyFileSync(resolve(root, 'clients/speed-reading/nginx.conf'), resolve(output, 'nginx.conf'));
const revision = '319b250c104539dc55a136077f2e5bf99c6c99e4';
execFileSync('git', ['diff', '--quiet', revision, '--', 'clients/speed-reading', 'services/speed-reading-service', 'tests/Unit/SpeedReading.Application.UnitTests'], { cwd: root });
if (execFileSync('git', ['rev-parse', revision], { cwd: root, encoding: 'utf8' }).trim() !== revision) {
  throw new Error('The release source commit is not available locally.');
}
if (!/^[a-f0-9]{40}$/.test(revision)) throw new Error('Could not resolve a full Git revision.');
writeFileSync(resolve(output, 'source-revision.txt'), `${revision}\n`);

const walk = directory => readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
  if (entry.isSymbolicLink()) throw new Error('Release payload must not contain symbolic links.');
  const path = resolve(directory, entry.name);
  return entry.isDirectory() ? walk(path) : [path];
});
const manifest = walk(output).filter(file => file !== resolve(output, 'payload.sha256')).sort().map(file => {
  const hash = createHash('sha256').update(readFileSync(file)).digest('hex');
  return `${hash}  ${relative(output, file).replaceAll('\\', '/')}`;
});
writeFileSync(resolve(output, 'payload.sha256'), `${manifest.join('\n')}\n`);
console.log(`Prepared ${manifest.length} checksummed files for Speed Reading revision ${revision}.`);
