import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { resolve } from 'node:path';

const root = fileURLToPath(new URL('../../../', import.meta.url));
export const packs = ['focus/v1', 'vocabulary/v1', 'free-reading/v1', 'error-analysis/v1', 'rsvp/v2'];
const readPack = (pack, file) => readFileSync(resolve(root, 'content-packs', pack, file), 'utf8');
const settings = packs.flatMap(pack => JSON.parse(readPack(pack, 'settings.json')));
const content = packs.slice(2).flatMap(pack => JSON.parse(readPack(pack, 'content.json')));
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
  (to_jsonb(e) - 'configuration_json' - 'title' - 'updated_at' - 'updated_by' - 'version')::text, '|'
  ORDER BY e.id),'')) FROM speed_reading.exercises e
WHERE id IN (${ids(settings.filter(s => s.previousConfiguration !== null))});`}
COMMIT;
`;
}

function buildVerificationSql() {
  const payload = JSON.stringify(settings);
  return `BEGIN READ ONLY;
DO $$ BEGIN
  IF (SELECT count(*) FROM jsonb_array_elements($manifest$${payload}$manifest$::jsonb) s
    JOIN speed_reading.exercises e ON e.id=(s->>'id')::uuid
    WHERE e.configuration_json=s->'configuration' AND e.is_active AND NOT e.is_deleted)<>88
    THEN RAISE EXCEPTION 'Release catalog verification failed'; END IF;
  IF (SELECT count(*) FROM speed_reading.reading_texts WHERE id IN (${ids(content)}) AND is_active AND NOT is_deleted)<>162
    OR (SELECT count(*) FROM speed_reading.reading_questions WHERE id IN (${ids(questions)}) AND NOT is_deleted)<>110
    THEN RAISE EXCEPTION 'Release content verification failed'; END IF;
END $$;
COMMIT;
`;
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  const output = resolve(process.argv[2] ?? 'artifacts/pending-speed-reading-20261011');
  const allowed = resolve(root, 'artifacts') + '\\';
  if (!output.startsWith(allowed)) throw new Error('Output must stay within workspace artifacts');
  if (settings.length !== 88 || content.length !== 162 || questions.length !== 110) throw new Error('Manifest counts changed');
  mkdirSync(output, { recursive: true });
  writeFileSync(resolve(output, 'apply-all.sql'), buildBatch());
  writeFileSync(resolve(output, 'inventory.sql'), buildInventorySql());
  writeFileSync(resolve(output, 'inventory-all.sql'), buildInventorySql(true));
  writeFileSync(resolve(output, 'verify-targets.sql'), buildVerificationSql());
  console.log('Prepared five atomic packages: 88 catalog entries, 162 texts, 110 questions.');
}
