import { cpSync, mkdirSync, readFileSync, writeFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const scripts = dirname(fileURLToPath(import.meta.url));
const root = resolve(scripts, '../../..');
const snapshot = resolve(root, '.tmp/coaching-release-ac014834');
const release = resolve(root, 'artifacts/coaching-live-20261013/package');
assert.ok(!existsSync(release) || readdirSync(release).length === 0, 'Package target must be empty; preserve the existing reviewed package.');
mkdirSync(release, { recursive: true });
for (const service of ['identity', 'coaching', 'notification']) {
  const source = resolve(snapshot, 'out', service);
  assert.ok(statSync(resolve(source, `${service === 'identity' ? 'Identity' : service === 'coaching' ? 'Coaching' : 'Notification'}.API.dll`)).size > 0);
  cpSync(source, resolve(release, 'payload', service), { recursive: true });
}
for (const [source, destination] of [['admin-panel', 'admin'], ['staff-portal/browser', 'staff']]) {
  cpSync(resolve(snapshot, 'clients/admin-panel/dist', source), resolve(release, 'payload', destination), { recursive: true });
}
for (const file of ['api.Dockerfile', 'admin.Dockerfile', 'staff.Dockerfile', 'deploy.sh', 'legacy-routes.sh', 'smoke.sh']) cpSync(resolve(scripts, file), resolve(release, file));
const revision = execFileSync('git', ['rev-parse', 'ac014834'], { cwd: root, encoding: 'utf8' }).trim();
writeFileSync(resolve(release, 'source-revision.txt'), revision + '\n');
const migrations = readdirSync(resolve(snapshot, 'services/coaching-service/Coaching.Infrastructure/Data/Migrations'))
  .filter(file => /^\d{14}_.*\.cs$/.test(file) && !file.endsWith('.Designer.cs')).map(file => file.slice(0, -3)).sort();
writeFileSync(resolve(release, 'expected-coaching-migrations.txt'), migrations.join('\n') + '\n');
writeFileSync(resolve(release, 'source-provenance.json'), JSON.stringify({ revision, staffBase: '0bffbe36', adminBase: '6f315ff3',
  identityLoginBase: 'google-direct-20261013; six sources match live portable PDB', excluded: 'unreleased course module',
  tests: { backend: 767, learnerAdmin: 308, teacherInstitution: 180, browser: 12 } }, null, 2) + '\n');
function files(path) { return readdirSync(path).flatMap(file => { const full = resolve(path, file); return statSync(full).isDirectory() ? files(full) : [full]; }); }
const manifest = files(release).filter(file => !file.endsWith('payload.sha256')).map(file =>
  `${createHash('sha256').update(readFileSync(file)).digest('hex')}  ${relative(release, file).replaceAll('\\', '/')}`);
assert.ok(files(release).every(file => !relative(release, file).split(/[\\/]/).some(part => part === '.env' || part.startsWith('.env.'))), 'Environment files must not enter the package.');
writeFileSync(resolve(release, 'payload.sha256'), manifest.join('\n') + '\n');
console.log(JSON.stringify({ release, revision, migrations: migrations.length, files: manifest.length }));
