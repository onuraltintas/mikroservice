import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';

const [snapshotFile, destination] = process.argv.slice(2);
if (!snapshotFile || !destination) throw new Error('Usage: prepare-release.mjs <snapshot.json> <release-directory>');
const snapshot = JSON.parse(fs.readFileSync(snapshotFile, 'utf8'));
const texts = JSON.parse(fs.readFileSync(new URL('./texts.json', import.meta.url), 'utf8'));
const patch = texts.map(text => {
  const live = snapshot.filter(item => item.name === text.name);
  assert.equal(live.length, 1, `Expected one active exercise type named ${text.name}`);
  const [current] = live;
  return { id: current.id, name: text.name, description: text.description, previousDescription: current.description,
    version: current.version, updatedAt: current.updatedAt, updatedBy: current.updatedBy };
});
const header = `BEGIN;
SELECT pg_advisory_xact_lock(hashtext('speed-reading-exercise-type-texts-v1'));
LOCK TABLE speed_reading.exercise_types IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE type_text_patch ON COMMIT DROP AS SELECT item FROM jsonb_array_elements($patch$${JSON.stringify(patch)}$patch$::jsonb) item;
DO $$ BEGIN
IF (SELECT count(*) FROM type_text_patch)<>${patch.length} THEN RAISE EXCEPTION 'Unexpected patch size'; END IF;
IF EXISTS(SELECT 1 FROM type_text_patch d LEFT JOIN speed_reading.exercise_types t ON t.id=(item->>'id')::uuid
 WHERE t.id IS NULL OR t.name<>item->>'name' OR NOT t.is_active OR t.is_deleted
 OR t.description NOT IN(item->>'previousDescription',item->>'description'))
 THEN RAISE EXCEPTION 'Exercise type text changed since the snapshot; take a fresh snapshot'; END IF;
END $$;
`;
const apply = `UPDATE speed_reading.exercise_types t SET description=item->>'description',updated_at=now(),updated_by='system:exercise-type-texts-v1',version=t.version+1
 FROM type_text_patch d WHERE t.id=(item->>'id')::uuid AND t.description<>item->>'description';
COMMIT;
`;
const rollback = `UPDATE speed_reading.exercise_types t SET description=item->>'previousDescription',updated_at=(item->>'updatedAt')::timestamptz,updated_by=item->>'updatedBy',version=(item->>'version')::int
 FROM type_text_patch d WHERE t.id=(item->>'id')::uuid AND t.description<>item->>'previousDescription';
COMMIT;
`;
fs.mkdirSync(destination, { recursive: true });
fs.writeFileSync(path.join(destination, 'apply.sql'), header + apply);
fs.writeFileSync(path.join(destination, 'rollback.sql'), header + rollback);
fs.copyFileSync(new URL('./snapshot.sql', import.meta.url), path.join(destination, 'snapshot.sql'));
const changed = patch.filter(item => item.previousDescription !== item.description).length;
console.log(JSON.stringify({ types: patch.length, changed }));
