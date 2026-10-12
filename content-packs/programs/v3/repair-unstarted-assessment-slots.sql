BEGIN;
SELECT pg_advisory_xact_lock(hashtext('speed-reading-programs-v3'));
CREATE TEMP TABLE repair_target AS SELECT :'repair_progress_id'::uuid id, :'repair_schedule_md5'::text expected_md5;
DO $$ BEGIN
IF NOT EXISTS(SELECT 1 FROM repair_target r JOIN speed_reading.student_program_progress p ON p.id=r.id
 WHERE p."ProgramTemplateId"='e2c01002-8d9a-4e6c-a428-000000000001'::uuid AND p."IsActive" AND p."CompletedDate" IS NULL)
 THEN RAISE EXCEPTION 'Unexpected repair assignment'; END IF;
IF EXISTS(SELECT 1 FROM repair_target r JOIN speed_reading.student_program_progress p ON p.id=r.id
 WHERE md5(p."ScheduleJson"::text)<>r.expected_md5 AND EXISTS(SELECT 1 FROM jsonb_array_elements(p."ScheduleJson"::jsonb) slot
 JOIN speed_reading.exercises e ON e.id=(slot->>'ExerciseId')::uuid WHERE e.is_assessment_only))
 THEN RAISE EXCEPTION 'Repair source schedule changed'; END IF;
END $$;
CREATE TEMP TABLE affected_training AS
SELECT p.id FROM speed_reading.student_program_progress p JOIN repair_target r ON r.id=p.id
WHERE p."ProgramTemplateId"='e2c01002-8d9a-4e6c-a428-000000000001'::uuid
  AND EXISTS(SELECT 1 FROM jsonb_array_elements(p."ScheduleJson"::jsonb) slot
    JOIN speed_reading.exercises e ON e.id=(slot->>'ExerciseId')::uuid WHERE e.is_assessment_only);
DO $$ BEGIN
IF (SELECT count(*) FROM affected_training)>1 THEN RAISE EXCEPTION 'Unexpected legacy cohort'; END IF;
IF EXISTS(SELECT 1 FROM affected_training a JOIN speed_reading.student_program_progress p ON p.id=a.id
 CROSS JOIN LATERAL jsonb_array_elements(p."ScheduleJson"::jsonb) slot
 LEFT JOIN speed_reading.exercises e ON e.id=(slot->>'ExerciseId')::uuid WHERE e.id IS NULL)
 THEN RAISE EXCEPTION 'Legacy schedule has an orphan slot'; END IF;
IF EXISTS(SELECT 1 FROM affected_training a JOIN speed_reading.student_program_progress p ON p.id=a.id
  WHERE p."DaysCompleted"<>0 OR p."ExercisesCompleted"<>0
  OR EXISTS(SELECT 1 FROM speed_reading.daily_exercise_logs l WHERE l."StudentProgramProgressId"=p.id)
  OR EXISTS(SELECT 1 FROM speed_reading.exercise_sessions s JOIN speed_reading.exercises e ON e.id=s.exercise_id
    WHERE s.student_id=p."UserId" AND e.is_assessment_only AND s.assessment_attempt_id IS NULL))
THEN RAISE EXCEPTION 'Legacy training has started; requires a slot-specific repair'; END IF;
END $$;
CREATE TEMP TABLE repaired_training AS
SELECT p.id,jsonb_agg(CASE WHEN old.is_assessment_only THEN jsonb_set(slot.item,'{ExerciseId}',to_jsonb(normal.id::text)) ELSE slot.item END ORDER BY slot.position) schedule
FROM affected_training a JOIN speed_reading.student_program_progress p ON p.id=a.id
CROSS JOIN LATERAL jsonb_array_elements(p."ScheduleJson"::jsonb) WITH ORDINALITY slot(item,position)
JOIN speed_reading.exercises old ON old.id=(slot.item->>'ExerciseId')::uuid
LEFT JOIN LATERAL(SELECT e.id FROM speed_reading.exercises e JOIN speed_reading.exercise_types t ON t.id=e.exercise_type_id
 WHERE e.exercise_type_id=old.exercise_type_id AND e.target_age_group_id=old.target_age_group_id
 AND e.difficulty_level=old.difficulty_level AND e.is_active AND NOT e.is_deleted AND NOT e.is_assessment_only
 AND (t.name<>'Comprehension' OR e.configuration_json #>> '{engineConfig,comprehensionMode}'=
    CASE WHEN (slot.item->>'DayNumber')::int=7 THEN 'evaluation' ELSE 'learning' END)
 ORDER BY e.id LIMIT 1) normal ON true
GROUP BY p.id;
DO $$ BEGIN
IF EXISTS(SELECT 1 FROM repaired_training r JOIN speed_reading.student_program_progress p ON p.id=r.id
 WHERE jsonb_array_length(r.schedule)<>jsonb_array_length(p."ScheduleJson"::jsonb))
 THEN RAISE EXCEPTION 'Repair changed slot count'; END IF;
IF EXISTS(SELECT 1 FROM repaired_training r CROSS JOIN LATERAL jsonb_array_elements(r.schedule::jsonb) slot
 WHERE slot->>'ExerciseId' IS NULL) THEN RAISE EXCEPTION 'Missing normal replacement'; END IF;
END $$;
UPDATE speed_reading.student_program_progress p SET "ScheduleJson"=r.schedule,
 updated_at=now(),updated_by='system:programs-v3-scope-repair',version=p.version+1
FROM repaired_training r WHERE r.id=p.id AND p."ScheduleJson"<>r.schedule;
COMMIT;
