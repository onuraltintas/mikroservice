#!/usr/bin/env node
// `npm audit --audit-level=high` with a short, reviewed list of advisories the build may carry for now.
// Usage: node tools/ci/npm-audit.mjs [npm audit arguments, e.g. --prefix ../course-management]
//
// Every high or critical advisory fails the build unless it is listed below with the reason and the date it was
// accepted. Remove an entry as soon as a fixed release exists.
import { spawnSync } from 'node:child_process';

const ACCEPTED = new Map([
  // User decision 18.11.2026: picomatch ReDoS. No fixed release exists (latest 4.0.7). picomatch is used only by
  // the build and test tools (Angular CLI, vite, vitest, fdir, tinyglobby); it is not part of any served bundle.
  ['GHSA-fc5q-39xc-vjpf', 'picomatch ReDoS, build and test tooling only, no fixed release (accepted 18.11.2026)']
]);
const BLOCKING = new Set(['high', 'critical']);

const run = spawnSync('npm', ['audit', '--json', ...process.argv.slice(2)], { encoding: 'utf8', shell: process.platform === 'win32' });
let report;
try {
  report = JSON.parse(run.stdout);
} catch {
  console.error(run.stdout, run.stderr);
  console.error('npm audit did not return a JSON report.');
  process.exit(1);
}
if (report.error) {
  console.error('npm audit failed:', report.error.summary ?? report.error);
  process.exit(1);
}

const advisories = new Map();
for (const vulnerability of Object.values(report.vulnerabilities ?? {})) {
  for (const source of vulnerability.via ?? []) {
    if (typeof source !== 'object' || !BLOCKING.has(source.severity)) continue;
    const id = String(source.url ?? '').split('/').pop() || String(source.source);
    advisories.set(id, `${source.name}: ${source.title} (${source.severity}) ${source.url ?? ''}`.trim());
  }
}

const accepted = [...advisories].filter(([id]) => ACCEPTED.has(id));
const blocking = [...advisories].filter(([id]) => !ACCEPTED.has(id));
for (const [id] of accepted) console.log(`Accepted for now: ${id} — ${ACCEPTED.get(id)}`);
if (blocking.length) {
  for (const [, text] of blocking) console.error(`Blocking advisory: ${text}`);
  process.exit(1);
}
console.log(`npm audit: no blocking high or critical advisory (${accepted.length} accepted).`);
