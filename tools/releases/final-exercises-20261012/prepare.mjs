import { readFileSync, writeFileSync, mkdirSync, readdirSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const root = fileURLToPath(new URL('../../../', import.meta.url));
export const packs = ['skimming/v1', 'eye-tracking/v1', 'exam-simulation/v1'];
const readPack = (pack, file) => readFileSync(resolve(root, 'content-packs', pack, file), 'utf8');
const settings = packs.flatMap(pack => JSON.parse(readPack(pack, 'settings.json')));
const content = JSON.parse(readPack('skimming/v1', 'content.json'));
const questions = content.flatMap(text => text.questions ?? []);
const ids = rows => rows.map(row => {
  if (!/^[a-f0-9-]{36}$/.test(row.id)) throw new Error('Invalid manifest UUID');
  return `'${row.id}'`;
}).join(',');

export function buildBatch(read = pack => readPack(pack, 'apply.sql')) {
  const bodies = packs.map(pack => {
    const sql = read(pack).replaceAll('\r\n', '\n');
    if ((sql.match(/^BEGIN;\s*$/gm) ?? []).length !== 1
      || (sql.match(/^COMMIT;\s*$/gm) ?? []).length !== 1) throw new Error('Invalid transaction boundaries');
    return `-- Package: ${pack}\n${sql.replace(/^BEGIN;\s*$/gm, '').replace(/^COMMIT;\s*$/gm, '')}`;
  });
  return `BEGIN;\n${bodies.join('\n')}\nCOMMIT;\n`;
}

export function buildInventorySql(all = false) {
  const filter = all ? "''" : `CASE tablename
    WHEN 'exercises' THEN ' WHERE id NOT IN (${ids(settings).replaceAll("'", "''")})'
    WHEN 'reading_texts' THEN ' WHERE id NOT IN (${ids(content).replaceAll("'", "''")})'
    WHEN 'reading_questions' THEN ' WHERE id NOT IN (${ids(questions).replaceAll("'", "''")})'
    ELSE '' END`;
  return `BEGIN READ ONLY;
SELECT format('SELECT %L || ''|'' || count(*) || ''|'' || md5(COALESCE(string_agg(row_to_json(t)::text, ''|'' ORDER BY row_to_json(t)::text), '''')) FROM %I.%I t%s;',
  schemaname || '.' || tablename,schemaname,tablename,${filter})
FROM pg_tables WHERE schemaname IN ('speed_reading','public') ORDER BY schemaname,tablename
\\gexec
${all ? '' : `SELECT 'target-exercises-immutable|' || count(*) || '|' || md5(COALESCE(string_agg(
  (to_jsonb(e) - 'configuration_json' - 'updated_at' - 'updated_by' - 'version')::text, '|'
  ORDER BY e.id),'')) FROM speed_reading.exercises e
WHERE id IN (${ids(settings.filter(s => s.previousConfiguration !== null))});`}
COMMIT;
`;
}

function buildVerificationSql(rollback = false) {
  const payload = JSON.stringify(settings);
  return `BEGIN READ ONLY;
DO $$ BEGIN
  IF (SELECT count(*) FROM jsonb_array_elements($manifest$${payload}$manifest$::jsonb) s
    JOIN speed_reading.exercises e ON e.id=(s->>'id')::uuid
    WHERE e.configuration_json=s->'${rollback ? 'previousConfiguration' : 'configuration'}' AND e.is_active AND NOT e.is_deleted)<>35
    THEN RAISE EXCEPTION 'Release catalog verification failed'; END IF;
  IF (SELECT count(*) FROM speed_reading.reading_texts WHERE id IN (${ids(content)}) AND ${rollback ? 'NOT ' : ''}is_active AND NOT is_deleted)<>36
    OR (SELECT count(*) FROM speed_reading.reading_questions WHERE id IN (${ids(questions)}) AND NOT is_deleted)<>144
    THEN RAISE EXCEPTION 'Release content verification failed'; END IF;
END $$;
COMMIT;
`;
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  const output = resolve(process.argv[2] ?? 'artifacts/final-exercises-20261012');
  const allowed = resolve(root, 'artifacts') + '\\';
  if (!output.startsWith(allowed)) throw new Error('Output must stay within workspace artifacts');
  if (settings.length !== 35 || content.length !== 36 || questions.length !== 144) throw new Error('Manifest counts changed');
  mkdirSync(output, { recursive: true });
  writeFileSync(resolve(output, 'apply-all.sql'), buildBatch());
  writeFileSync(resolve(output, 'inventory.sql'), buildInventorySql());
  writeFileSync(resolve(output, 'inventory-all.sql'), buildInventorySql(true));
  writeFileSync(resolve(output, 'verify-targets.sql'), buildVerificationSql());
  writeFileSync(resolve(output, 'rollback-all.sql'), buildBatch(pack => readPack(pack, 'rollback.sql')));
  writeFileSync(resolve(output, 'verify-rollback.sql'), buildVerificationSql(true));
  if (process.argv.includes('--seal')) {
    for (const file of ['deploy.sh', 'monitor.sh', 'api.Dockerfile', 'web.Dockerfile', 'production.override.yml', 'README.md']) {
      writeFileSync(resolve(output, file), readFileSync(new URL(file, import.meta.url), 'utf8').replaceAll('\r\n', '\n'));
    }
    writeFileSync(resolve(output, 'nginx.conf'), readFileSync(resolve(root, 'clients/speed-reading/nginx.conf'), 'utf8').replaceAll('\r\n', '\n'));
    writeFileSync(resolve(output, 'source-revision.txt'), execFileSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim() + '\n');
    const walk = directory => readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
      if (entry.isSymbolicLink()) throw new Error('Payload must not contain symlinks');
      const path = resolve(directory, entry.name);
      return entry.isDirectory() ? walk(path) : [path];
    });
    const lines = walk(output).filter(file => file !== resolve(output, 'payload.sha256')).sort().map(file =>
      `${createHash('sha256').update(readFileSync(file)).digest('hex')}  ${file.slice(output.length + 1).replaceAll('\\', '/')}`);
    writeFileSync(resolve(output, 'payload.sha256'), lines.join('\n') + '\n');
    console.log(`Sealed ${lines.length} generated/published payload files; no production environment file included.`);
  }
  console.log('Prepared three atomic packages: 35 catalog entries, 36 texts, 144 questions.');
}
