import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const dir = path.dirname(fileURLToPath(import.meta.url));
const baselines = ['child', 'teen', 'youngadult', 'adult'].flatMap(age =>
  JSON.parse(fs.readFileSync(path.join(dir, `baseline-${age}.json`), 'utf8')).programs);
const base = baselines.find(p => p.age === 'Child' && p.level === 1).pattern;
const programs = baselines.map(p => {
  const pattern = structuredClone(base);
  const replace = (day, from, to) => {
    const entries = pattern[`week${Math.floor((day - 1) / 7) + 1}`][`day${(day - 1) % 7 + 1}`];
    const slot = entries.find(x => x.type === from);
    if (!slot) throw new Error(`${p.age} S${p.level}: day ${day} has no ${from}`);
    slot.type = to;
  };
  replace(13, 'Comprehension', 'Visualization');
  replace(25, 'Comprehension', 'Visualization');
  replace(19, 'SpeedReading', 'TextFading');
  if (p.level >= 3) {
    replace(27, 'SpeedReading', 'TextFading');
    replace(18, 'Scanning', 'Skimming');
    replace(25, 'Scanning', 'Skimming');
  }
  if (p.level >= (p.age === 'Child' ? 4 : 3)) {
    replace(17, 'SpeedReading', 'RSVP');
    replace(22, 'SpeedReading', 'RSVP');
  }
  if (p.level >= 4) {
    replace(20, 'Scanning', 'ErrorAnalysis');
    replace(27, 'Vocabulary', 'ErrorAnalysis');
  }
  if (p.level >= (p.age === 'Child' ? 2 : 1)) {
    replace(21, 'Chunking', 'RegressionReduction');
    replace(26, 'SpeedReading', 'RegressionReduction');
  }
  if (p.level >= (p.age === 'Child' ? 4 : p.age === 'Teen' ? 3 : 2)) {
    replace(23, 'SpeedReading', 'SubvocalizationReduction');
    replace(28, 'Chunking', 'SubvocalizationReduction');
  }
  for (let day = 1; day <= 28; day++) {
    const entries = pattern[`week${Math.floor((day - 1) / 7) + 1}`][`day${(day - 1) % 7 + 1}`];
    let evaluationAssigned = false;
    for (const slot of entries) {
      slot.difficulty = p.level;
      if (slot.type === 'Comprehension') {
        slot.comprehensionMode = day % 7 === 0 && !evaluationAssigned ? 'evaluation' : 'learning';
        evaluationAssigned ||= slot.comprehensionMode === 'evaluation';
      }
    }
  }
  const metadata = Object.fromEntries(Object.entries(p.pattern).filter(([key]) => !key.startsWith('week')));
  return { ...p, previousPattern: p.pattern, pattern: { ...metadata, ...Object.fromEntries(Object.entries(pattern).filter(([key]) => key.startsWith('week'))) } };
});
const json = JSON.stringify(programs, null, 2) + '\n';
fs.writeFileSync(path.join(dir, 'programs.json'), json);
fs.writeFileSync(path.join(dir, 'apply.sql'), `BEGIN;
SELECT pg_advisory_xact_lock(hashtext('speed-reading-programs-v3'));
CREATE TEMP TABLE desired_programs AS SELECT item FROM jsonb_array_elements($programs$${JSON.stringify(programs)}$programs$::jsonb) item;
DO $$ BEGIN
IF (SELECT count(*) FROM desired_programs) <> 20 OR (SELECT count(DISTINCT item->>'id') FROM desired_programs) <> 20 THEN RAISE EXCEPTION 'Expected 20 unique programs'; END IF;
IF EXISTS (SELECT 1 FROM desired_programs d LEFT JOIN speed_reading.program_templates p ON p.id=(item->>'id')::uuid
 WHERE p.id IS NULL OR p."IsAssessment" OR NOT p."IsActive" OR p."IsDeleted"
 OR p."TargetAgeGroupConfigurationId"<>(item->>'ageId')::uuid OR p."InitialDifficultyLevel"<>(item->>'level')::int
 OR p."TotalDays"<>28 OR p."TotalWeeks"<>4
 OR p."WeeklyPatternJson" NOT IN (item->'previousPattern',item->'pattern')) THEN RAISE EXCEPTION 'Unexpected source program; no forced overwrite'; END IF;
IF EXISTS (SELECT 1 FROM desired_programs d CROSS JOIN LATERAL jsonb_each(item->'pattern') w
 CROSS JOIN LATERAL jsonb_each(CASE WHEN w.key LIKE 'week%' THEN w.value ELSE '{}'::jsonb END) day
 CROSS JOIN LATERAL jsonb_array_elements(day.value) slot
 WHERE NOT EXISTS (SELECT 1 FROM speed_reading.exercises e JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
 WHERE e.is_active AND NOT e.is_deleted AND NOT e.is_assessment_only AND t.is_active AND NOT t.is_deleted
 AND e.target_age_group_id=(item->>'ageId')::uuid AND e.difficulty_level=(slot->>'difficulty')::int AND t.name=slot->>'type'
 AND (slot->>'comprehensionMode' IS NULL OR e.configuration_json #>> '{engineConfig,comprehensionMode}'=slot->>'comprehensionMode')))
 THEN RAISE EXCEPTION 'Missing exact normal age/type/level/mode'; END IF;
END $$;
UPDATE speed_reading.program_templates p SET "WeeklyPatternJson"=d.item->'pattern',updated_at=now(),updated_by='system:programs-v3',version=p.version+1
 FROM desired_programs d WHERE p.id=(item->>'id')::uuid AND p."WeeklyPatternJson"<>item->'pattern';
COMMIT;
`);
fs.writeFileSync(path.join(dir, 'rollback.sql'), `BEGIN;
SELECT pg_advisory_xact_lock(hashtext('speed-reading-programs-v3'));
CREATE TEMP TABLE previous_programs AS SELECT item FROM jsonb_array_elements($programs$${JSON.stringify(programs)}$programs$::jsonb) item;
DO $$ BEGIN
IF EXISTS (SELECT 1 FROM previous_programs d LEFT JOIN speed_reading.program_templates p ON p.id=(item->>'id')::uuid
 WHERE p.id IS NULL OR p."IsAssessment" OR p."WeeklyPatternJson" NOT IN(item->'previousPattern',item->'pattern')) THEN RAISE EXCEPTION 'Unexpected rollback source'; END IF;
END $$;
UPDATE speed_reading.program_templates p SET "WeeklyPatternJson"=d.item->'previousPattern',updated_at=now(),updated_by='system:programs-v3-rollback',version=p.version+1
 FROM previous_programs d WHERE p.id=(item->>'id')::uuid AND p."WeeklyPatternJson"<>item->'previousPattern';
COMMIT;
`);
