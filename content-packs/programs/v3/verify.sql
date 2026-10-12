BEGIN READ ONLY;
DO $$ BEGIN
IF (SELECT count(*) FROM speed_reading.program_templates WHERE "IsActive" AND NOT "IsDeleted" AND NOT "IsAssessment")<>20 THEN RAISE EXCEPTION 'Expected 20 training programs'; END IF;
IF (SELECT count(*) FROM speed_reading.exercises WHERE is_assessment_only AND is_active AND NOT is_deleted)<>12 THEN RAISE EXCEPTION 'Expected 12 scoped assessment records'; END IF;
IF (SELECT count(*) FROM speed_reading.program_templates WHERE "IsAssessment" AND "IsActive" AND NOT "IsDeleted")<>4 THEN RAISE EXCEPTION 'Assessment template changed'; END IF;
IF EXISTS(SELECT 1 FROM speed_reading.program_templates p CROSS JOIN LATERAL jsonb_each(p."WeeklyPatternJson") week
 CROSS JOIN LATERAL jsonb_each(CASE WHEN week.key LIKE 'week%' THEN week.value ELSE '{}'::jsonb END) day
 CROSS JOIN LATERAL jsonb_array_elements(day.value) slot WHERE NOT p."IsAssessment" AND p."IsActive" AND NOT p."IsDeleted" AND slot->>'type'='ExamSimulation') THEN RAISE EXCEPTION 'Exam simulation must not be assigned'; END IF;
IF (SELECT count(*) FROM speed_reading.reading_texts WHERE id::text LIKE 'fa031200-0000-5000-8000-%' AND is_active AND NOT is_deleted)<>15
 OR (SELECT count(*) FROM speed_reading.reading_questions WHERE id::text LIKE 'fa031201-0000-5000-8000-%' AND NOT is_deleted)<>60 THEN RAISE EXCEPTION 'Missing short content'; END IF;
IF EXISTS(SELECT 1 FROM speed_reading.student_program_progress p CROSS JOIN LATERAL jsonb_array_elements(p."ScheduleJson"::jsonb) slot
 JOIN speed_reading.exercises e ON e.id=(slot->>'ExerciseId')::uuid WHERE e.is_assessment_only) THEN RAISE EXCEPTION 'Unrepaired assessment-only training slot'; END IF;
END $$;
SELECT jsonb_build_object('Programs',20,'Days',560,'Slots',3640,'ShortTexts',15,'Questions',60,'ExamSlots',0);
COMMIT;
