#!/usr/bin/env node
// KM8 (01.11.2026): writes the 141-lesson list from lessons.csv into check.sql, apply.sql, verify.sql and restore.sql,
// between the "-- lesson list: begin" and "-- lesson list: end" lines.
//   node build.mjs           rewrites the lists
//   node build.mjs --check   exits 1 when a file's list differs from lessons.csv
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const dir = fileURLToPath(new URL('.', import.meta.url));
const [header, ...rows] = readFileSync(dir + 'lessons.csv', 'utf8').trim().split('\n');
if (header !== 'Id,Name') throw new Error('lessons.csv must have the header Id,Name');
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
const lessons = rows.map(line => {
  const comma = line.indexOf(',');
  const [id, name] = [line.slice(0, comma), line.slice(comma + 1)];
  if (!uuid.test(id) || !name || name.includes('"')) throw new Error(`bad row: ${line}`);
  return { id, name };
});
if (new Set(lessons.map(x => x.id)).size !== lessons.length) throw new Error('duplicate lesson id');
const block = lessons.map((x, i) => `    ('${x.id}'::uuid, '${x.name.replaceAll("'", "''")}')${i === lessons.length - 1 ? '' : ','}`).join('\n');

const check = process.argv.includes('--check');
let stale = [];
for (const file of ['check.sql', 'apply.sql', 'verify.sql', 'restore.sql']) {
  const text = readFileSync(dir + file, 'utf8');
  const next = text.replace(/(-- lesson list: begin[^\n]*\n)[\s\S]*?(^[ \t]*-- lesson list: end)/m, `$1${block}\n$2`);
  if (!/-- lesson list: begin/.test(text)) throw new Error(`${file} has no lesson list block`);
  if (next !== text) { stale.push(file); if (!check) writeFileSync(dir + file, next); }
}
if (check && stale.length) { console.error(`out of date (run node build.mjs): ${stale.join(', ')}`); process.exit(1); }
if (!check) console.log(`${lessons.length} lessons; updated: ${stale.join(', ') || 'none'}`);
