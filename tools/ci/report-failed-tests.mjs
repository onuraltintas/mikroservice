#!/usr/bin/env node
// Repeats failing Vitest tests (and unhandled errors that fail an otherwise green run) as GitHub annotations, because
// job logs are not always reachable from review tooling. Usage: node tools/ci/report-failed-tests.mjs <log>...
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ansi = /\u001b\[[0-9;]*m/g;
const separator = /^\s*⎯{3,}/;
const maxBodyLines = 12;

export function extractFailures(text) {
  const lines = text.replace(ansi, '').split(/\r?\n/);
  const failures = [];
  for (let index = 0; index < lines.length; index++) {
    const line = lines[index];
    const fail = /^\s*FAIL\s+(.*\S)\s*$/.exec(line);
    if (fail) {
      failures.push({ title: fail[1].replace(/\s{2,}/g, ' '), body: collect(lines, index + 1) });
      continue;
    }
    if (/^\s*⎯+\s*(Uncaught Exception|Unhandled Rejection|Unhandled Error)\s*⎯+/.test(line)) {
      failures.push({ title: 'Unhandled error during the test run', body: collect(lines, index + 1, true) });
    }
  }
  return failures;
}

function collect(lines, start, includeOrigin = false) {
  const body = [];
  for (let index = start; index < lines.length && body.length < maxBodyLines; index++) {
    const line = lines[index];
    if (separator.test(line) || /^\s*FAIL\s/.test(line)) break;
    const trimmed = line.trimEnd();
    if (!trimmed.trim()) continue;
    // Keep the message, the expected/received lines and the source location; skip the code excerpt.
    if (/^\s+\d+\|/.test(trimmed) || /^\s+\|/.test(trimmed)) continue;
    if (!includeOrigin && /^If this is a long-running test/.test(trimmed.trim())) continue;
    body.push(trimmed.trim());
  }
  return body.join('\n');
}

const escapeData = value => value.replace(/%/g, '%25').replace(/\r/g, '%0D').replace(/\n/g, '%0A');
const escapeProperty = value => escapeData(value).replace(/:/g, '%3A').replace(/,/g, '%2C');

export function toAnnotations(failures, source) {
  return failures.slice(0, 10).map(item =>
    `::error title=${escapeProperty(`${source}: ${item.title}`)}::${escapeData(item.body.slice(0, 1800))}`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  for (const file of process.argv.slice(2)) {
    if (!existsSync(file)) continue;
    const failures = extractFailures(readFileSync(file, 'utf8'));
    for (const line of toAnnotations(failures, path.basename(file))) console.log(line);
    if (failures.length === 0) console.log(`No failing test found in ${path.basename(file)}; see the step log.`);
  }
}
