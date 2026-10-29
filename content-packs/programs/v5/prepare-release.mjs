import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { reviseAssignedSchedule } from './rules.mjs';

const [snapshotFile, destination] = process.argv.slice(2);
if (!snapshotFile || !destination) throw new Error('Usage: prepare-release.mjs <snapshot.json> <release-directory>');
const snapshot = JSON.parse(fs.readFileSync(snapshotFile, 'utf8'));
const plans = JSON.parse(fs.readFileSync(new URL('./programs.json', import.meta.url), 'utf8'));
assert.equal(snapshot.templates.length, 20);
const programs = plans.map(plan => {
  const previous = snapshot.templates.find(item => item.id === plan.id);
  assert.ok(previous, `Missing source ${plan.id}`);
  assert.equal(previous.ageId, plan.ageId);
  assert.equal(previous.level, plan.level);
  assert.deepEqual(previous.pattern, plan.previousPattern, `Live pattern changed: ${plan.id}`);
  for (const week of Object.values(plan.pattern).filter(value => value && typeof value === 'object')) {
    for (const day of Object.values(week)) for (const slot of day) {
      assert.ok(snapshot.exercises.some(exercise => exercise.type === slot.type && exercise.ageId === plan.ageId
        && exercise.level === slot.difficulty && (!slot.comprehensionMode || exercise.mode === slot.comprehensionMode)), `Unavailable ${plan.age}/${slot.type}/${slot.difficulty}`);
    }
  }
  return { ...previous, pattern: plan.pattern, previousPattern: previous.pattern };
});
const progress = snapshot.progress.flatMap(item => {
  const program = plans.find(plan => plan.id === item.templateId);
  assert.ok(program, 'Unexpected active program');
  const schedule = reviseAssignedSchedule(item, program, snapshot.exercises);
  if (!schedule) return [];
  assert.equal(schedule.length, item.schedule.length);
  for (const day of item.protectedDays) {
    const inDay = slot => (slot.WeekNumber - 1) * 7 + slot.DayNumber === day;
    assert.deepEqual(schedule.filter(inDay), item.schedule.filter(inDay));
  }
  return [{ ...item, previousSchedule: item.schedule, schedule }];
});
const header = `BEGIN;
SELECT pg_advisory_xact_lock(hashtext('speed-reading-program-target-hunt-v5'));
LOCK TABLE speed_reading.program_templates, speed_reading.student_program_progress, speed_reading.daily_exercise_logs, speed_reading.exercise_sessions IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE program_patch AS SELECT item FROM jsonb_array_elements($programs$${JSON.stringify(programs)}$programs$::jsonb) item;
CREATE TEMP TABLE progress_patch AS SELECT item FROM jsonb_array_elements($progress$${JSON.stringify(progress)}$progress$::jsonb) item;
DO $$ BEGIN
IF (SELECT count(*) FROM program_patch)<>20 THEN RAISE EXCEPTION 'Expected 20 programs'; END IF;
IF (SELECT count(*) FROM speed_reading.program_templates WHERE "IsActive" AND NOT "IsDeleted" AND NOT "IsAssessment")<>20
 THEN RAISE EXCEPTION 'Active program set changed; take a fresh snapshot'; END IF;
IF EXISTS(SELECT 1 FROM speed_reading.student_program_progress p JOIN program_patch d ON p."ProgramTemplateId"=(item->>'id')::uuid
 WHERE p."IsActive" AND p."CompletedDate" IS NULL AND p.id NOT IN(${snapshot.progress.map(item => `'${item.id}'::uuid`).join(',') || 'NULL'}))
 THEN RAISE EXCEPTION 'New active assignment; take a fresh snapshot'; END IF;
IF EXISTS(SELECT 1 FROM program_patch d LEFT JOIN speed_reading.program_templates p ON p.id=(item->>'id')::uuid
 WHERE p.id IS NULL OR p."IsAssessment" OR NOT p."IsActive" OR p."IsDeleted" OR p."TotalDays"<>28 OR p."TotalWeeks"<>4
 OR p."TargetAgeGroupConfigurationId"<>(item->>'ageId')::uuid OR p."InitialDifficultyLevel"<>(item->>'level')::int
 OR p."WeeklyPatternJson" NOT IN(item->'previousPattern',item->'pattern')) THEN RAISE EXCEPTION 'Program source drift'; END IF;
IF EXISTS(SELECT 1 FROM progress_patch d LEFT JOIN speed_reading.student_program_progress p ON p.id=(item->>'id')::uuid
 WHERE p.id IS NULL OR NOT p."IsActive" OR p."CompletedDate" IS NOT NULL OR p."ProgramTemplateId"<>(item->>'templateId')::uuid
 OR p."CurrentWeek"<>(item->>'currentWeek')::int OR p."CurrentDay"<>(item->>'currentDay')::int
 OR p."DaysCompleted"<>(item->>'daysCompleted')::int OR p."ExercisesCompleted"<>(item->>'exercisesCompleted')::int
 OR p."ScheduleJson"::jsonb NOT IN(item->'previousSchedule',item->'schedule')
 OR (SELECT md5(coalesce(string_agg(md5(to_jsonb(l)::text),'' ORDER BY l.id),'')) FROM speed_reading.daily_exercise_logs l WHERE l."StudentProgramProgressId"=p.id)<>item->>'logsFingerprint'
 OR (SELECT md5(coalesce(string_agg(md5(to_jsonb(s)::text),'' ORDER BY s.id),'')) FROM speed_reading.exercise_sessions s WHERE s.student_id=p."UserId" AND s.assessment_attempt_id IS NULL AND s.start_time>=p."AssignedDate")<>item->>'sessionsFingerprint')
 THEN RAISE EXCEPTION 'Training activity changed; take a fresh snapshot'; END IF;
END $$;
`;
const updates = `UPDATE speed_reading.program_templates p SET "WeeklyPatternJson"=item->'pattern',updated_at=now(),updated_by='system:program-target-hunt-v5',version=p.version+1
 FROM program_patch d WHERE p.id=(item->>'id')::uuid AND p."WeeklyPatternJson"<>item->'pattern';
UPDATE speed_reading.student_program_progress p SET "ScheduleJson"=item->'schedule',updated_at=now(),updated_by='system:program-target-hunt-v5',version=p.version+1
 FROM progress_patch d WHERE p.id=(item->>'id')::uuid AND p."ScheduleJson"::jsonb<>item->'schedule';
COMMIT;
`;
const rollback = `UPDATE speed_reading.program_templates p SET "WeeklyPatternJson"=item->'previousPattern',updated_at=(item->>'updatedAt')::timestamptz,updated_by=item->>'updatedBy',version=(item->>'version')::int
 FROM program_patch d WHERE p.id=(item->>'id')::uuid AND p."WeeklyPatternJson"<>item->'previousPattern';
UPDATE speed_reading.student_program_progress p SET "ScheduleJson"=item->'previousSchedule',updated_at=(item->>'updatedAt')::timestamptz,updated_by=item->>'updatedBy',version=(item->>'version')::int
 FROM progress_patch d WHERE p.id=(item->>'id')::uuid AND p."ScheduleJson"::jsonb<>item->'previousSchedule';
COMMIT;
`;
const progressIds = progress.map(item => `'${item.id}'`).join(',') || 'NULL';
const programIds = programs.map(item => `'${item.id}'`).join(',');
const inventory = `CREATE TEMP TABLE protected_inventory(table_name text,records bigint,fingerprint text);
BEGIN READ ONLY;
DO $$ DECLARE r record; expression text; BEGIN
FOR r IN SELECT table_name FROM information_schema.tables WHERE table_schema='speed_reading' AND table_type='BASE TABLE' ORDER BY table_name LOOP
 expression:='to_jsonb(t)';
 IF r.table_name='program_templates' THEN expression:='(to_jsonb(t)-CASE WHEN id IN(${programIds.replaceAll("'", "''")}) THEN ARRAY[''WeeklyPatternJson'',''updated_at'',''updated_by'',''version''] ELSE ARRAY[]::text[] END)';
 ELSIF r.table_name='student_program_progress' THEN expression:='(to_jsonb(t)-CASE WHEN id IN(${progressIds.replaceAll("'", "''")}) THEN ARRAY[''ScheduleJson'',''updated_at'',''updated_by'',''version''] ELSE ARRAY[]::text[] END)';
 END IF;
 EXECUTE format('INSERT INTO protected_inventory SELECT %L,count(*),md5(coalesce(string_agg(h,'''' ORDER BY h),'''')) FROM (SELECT md5((%s)::text) h FROM speed_reading.%I t) rows',r.table_name,expression,r.table_name);
END LOOP; END $$;
SELECT table_name||'|'||records||'|'||fingerprint FROM protected_inventory ORDER BY table_name;
COMMIT;
`;
fs.mkdirSync(destination, { recursive: true });
fs.writeFileSync(path.join(destination, 'apply.sql'), header + updates);
fs.writeFileSync(path.join(destination, 'rollback.sql'), header + rollback);
fs.writeFileSync(path.join(destination, 'inventory-protected.sql'), inventory);
fs.copyFileSync(new URL('../v3/inventory-all.sql', import.meta.url), path.join(destination, 'inventory-all.sql'));
fs.copyFileSync(new URL('./inventory.sql', import.meta.url), path.join(destination, 'inventory.sql'));
for (const file of ['snapshot.sql', 'verify.sql', 'verify-release.mjs', 'rules.mjs', 'programs.json'])
  fs.copyFileSync(new URL(`./${file}`, import.meta.url), path.join(destination, file));
console.log(JSON.stringify({ programs: programs.length, assignments: progress.length, protectedDays: progress.reduce((sum, item) => sum + item.protectedDays.length, 0) }));
