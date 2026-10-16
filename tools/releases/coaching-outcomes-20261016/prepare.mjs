import { cpSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { resolve } from 'node:path';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const root = process.cwd();
const releaseName = 'coaching-outcomes-live-20261016-r2';
const release = resolve(root, 'artifacts', releaseName);
const previous = resolve(root, 'artifacts/coaching-learning-live-20261016');
const revision = '374f79e2';
assert.ok(!existsSync(resolve(release, 'source')), 'Preserve existing reviewed sources.');
mkdirSync(release, { recursive: true });
const git = (...args) => execFileSync('git', args, { cwd: root, maxBuffer: 32 * 1024 * 1024 });
const files = git('diff', '--name-only', '1b7d8391', revision, '--', 'services/coaching-service', 'clients/admin-panel/src').toString().trim().split('\n');
for (const [kind, base, prefix] of [['source', 'source', 'services/'], ['admin-source', 'admin-source', 'clients/'], ['staff-source', 'staff-source', 'clients/']]) {
  const destination = resolve(release, kind);
  cpSync(resolve(previous, base), destination, { recursive: true,
    filter: path => !/[\\/](node_modules|dist|\.angular|bin|obj|\.git)([\\/]|$)/.test(path) });
  for (const file of files.filter(x => x.startsWith(prefix))) {
    if (file.endsWith('.spec.ts')) continue;
    if (kind === 'staff-source') {
      if (file.endsWith('/coaching-study-planning.service.ts')) {
        const target = resolve(destination, file);
        const anchor = '  completedAt: string | null;';
        const text = readFileSync(target, 'utf8'); assert.ok(text.includes(anchor));
        const added = git('show', revision + ':' + file).toString().split('\n').find(x => x.trim().startsWith('outcomes?:'));
        assert.ok(added); writeFileSync(target, text.replace(anchor, anchor + '\n' + added));
        continue;
      }
      if (!file.endsWith('/student-study-plans.component.html')) continue;
    }
    // Apply only the feature delta to each exact live baseline, retaining live-only fixes.
    const patch = git('diff', '--binary', '1b7d8391', revision, '--', file);
    execFileSync('git', ['apply', '--recount', '--directory=' + 'artifacts/' + releaseName + '/' + kind, '-'], { cwd: root, input: patch });
  }
}
const provenance = { revision: git('rev-parse', revision).toString().trim(), base: 'coaching-learning-20261016 release-r6',
  scope: 'Coaching API, admin/student UI and shared staff plan view', files,
  baselineCoachingAssemblies: Object.fromEntries(['Coaching.API','Coaching.Application','Coaching.Domain','Coaching.Infrastructure'].map(name =>
    [name, createHash('sha256').update(readFileSync(resolve(previous, 'publish/coaching', name + '.dll'))).digest('hex')])) };
writeFileSync(resolve(release, 'source-provenance.json'), JSON.stringify(provenance, null, 2));
console.log(JSON.stringify({ release, changedFiles: files.length, revision: provenance.revision }));
